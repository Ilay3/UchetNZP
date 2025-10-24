using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class WipService : IWipService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public WipService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<ReceiptBatchSummaryDto> AddReceiptsBatchAsync(IEnumerable<ReceiptItemDto> items, CancellationToken cancellationToken = default)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var materialized = items.ToList();
        if (materialized.Count == 0)
        {
            return new ReceiptBatchSummaryDto(0, Array.Empty<ReceiptItemSummaryDto>());
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var results = new List<ReceiptItemSummaryDto>(materialized.Count);

            foreach (var item in materialized)
            {
                var now = DateTime.UtcNow;
                var userId = _currentUserService.UserId;
                var receiptDate = NormalizeToUtc(item.ReceiptDate);

                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException($"Количество для детали {item.PartId} и операции {item.OpNumber} должно быть больше нуля.");
                }

                var route = await _dbContext.PartRoutes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PartId == item.PartId && x.OpNumber == item.OpNumber, cancellationToken)
                    .ConfigureAwait(false);

                if (route is null)
                {
                    throw new InvalidOperationException($"Маршрут для детали {item.PartId} и операции {item.OpNumber} не найден.");
                }

                if (route.SectionId != item.SectionId)
                {
                    throw new InvalidOperationException($"Операция {item.OpNumber} для детали {item.PartId} относится к виду работ {route.SectionId}, а не к {item.SectionId}.");
                }

                var balance = await _dbContext.WipBalances
                    .FirstOrDefaultAsync(
                        x => x.PartId == item.PartId && x.OpNumber == item.OpNumber && x.SectionId == item.SectionId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (balance is null)
                {
                    balance = new WipBalance
                    {
                        Id = Guid.NewGuid(),
                        PartId = item.PartId,
                        OpNumber = item.OpNumber,
                        SectionId = item.SectionId,
                        Quantity = 0m,
                    };

                    await _dbContext.WipBalances.AddAsync(balance, cancellationToken).ConfigureAwait(false);
                }

                var was = balance.Quantity;
                balance.Quantity = was + item.Quantity;

                var receipt = new WipReceipt
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PartId = item.PartId,
                    SectionId = item.SectionId,
                    OpNumber = item.OpNumber,
                    ReceiptDate = receiptDate,
                    CreatedAt = now,
                    Quantity = item.Quantity,
                    Comment = item.Comment,
                };

                await _dbContext.WipReceipts.AddAsync(receipt, cancellationToken).ConfigureAwait(false);

                results.Add(new ReceiptItemSummaryDto(
                    item.PartId,
                    item.OpNumber,
                    item.SectionId,
                    item.Quantity,
                    was,
                    balance.Quantity,
                    balance.Id,
                    receipt.Id));
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ReceiptBatchSummaryDto(results.Count, results);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ReceiptDeleteResultDto> DeleteReceiptAsync(Guid in_receiptId, CancellationToken cancellationToken = default)
    {
        if (in_receiptId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор прихода не задан.", nameof(in_receiptId));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        ReceiptDeleteResultDto ret;

        try
        {
            var receipt = await _dbContext.WipReceipts
                .FirstOrDefaultAsync(x => x.Id == in_receiptId, cancellationToken)
                .ConfigureAwait(false);

            if (receipt is null)
            {
                throw new KeyNotFoundException($"Приход с идентификатором {in_receiptId} не найден.");
            }

            var balance = await _dbContext.WipBalances
                .FirstOrDefaultAsync(
                    x => x.PartId == receipt.PartId &&
                        x.SectionId == receipt.SectionId &&
                        x.OpNumber == receipt.OpNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            if (balance is null)
            {
                throw new InvalidOperationException("Связанный остаток не найден.");
            }

            var previousQuantity = balance.Quantity;
            var restoredQuantity = previousQuantity - receipt.Quantity;

            if (restoredQuantity < 0)
            {
                throw new InvalidOperationException("Отмена прихода приведёт к отрицательному остатку.");
            }

            balance.Quantity = restoredQuantity;

            var adjustment = new WipBalanceAdjustment
            {
                Id = Guid.NewGuid(),
                WipBalanceId = balance.Id,
                PartId = balance.PartId,
                SectionId = balance.SectionId,
                OpNumber = balance.OpNumber,
                PreviousQuantity = previousQuantity,
                NewQuantity = restoredQuantity,
                Delta = restoredQuantity - previousQuantity,
                Comment = $"Отмена прихода {receipt.Id}",
                UserId = _currentUserService.UserId,
                CreatedAt = DateTime.UtcNow,
            };

            _dbContext.WipBalanceAdjustments.Add(adjustment);
            _dbContext.WipReceipts.Remove(receipt);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            ret = new ReceiptDeleteResultDto(
                receipt.Id,
                balance.Id,
                balance.PartId,
                balance.SectionId,
                balance.OpNumber,
                receipt.Quantity,
                previousQuantity,
                restoredQuantity,
                adjustment.Delta);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return ret;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
