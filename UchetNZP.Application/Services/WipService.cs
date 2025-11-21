using System;
using System.Collections.Generic;
using System.Globalization;
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

    public async Task<ReceiptBatchSummaryDto> AddReceiptsBatchAsync(IEnumerable<ReceiptItemDto> in_items, CancellationToken cancellationToken = default)
    {
        if (in_items is null)
        {
            throw new ArgumentNullException(nameof(in_items));
        }

        var materialized = in_items.ToList();
        if (materialized.Count == 0)
        {
            return new ReceiptBatchSummaryDto(0, Array.Empty<ReceiptItemSummaryDto>());
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var results = new List<ReceiptItemSummaryDto>(materialized.Count);
            var reservedLabelIds = new HashSet<Guid>();
            var balancesCache = new Dictionary<(Guid PartId, Guid SectionId, int OpNumber), WipBalance>();

            foreach (var item in materialized)
            {
                var now = DateTime.UtcNow;
                var userId = _currentUserService.UserId;
                var receiptDate = NormalizeToUtc(item.ReceiptDate);
                var balanceKey = (item.PartId, item.SectionId, item.OpNumber);

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

                if (!balancesCache.TryGetValue(balanceKey, out var balance))
                {
                    balance = await _dbContext.WipBalances
                        .FirstOrDefaultAsync(
                            x => x.PartId == item.PartId && x.OpNumber == item.OpNumber && x.SectionId == item.SectionId,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (balance is not null)
                    {
                        balancesCache[balanceKey] = balance;
                    }
                }

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
                    balancesCache[balanceKey] = balance;
                }

                var was = balance.Quantity;
                balance.Quantity = was + item.Quantity;

                var label = await ResolveLabelAsync(item, reservedLabelIds, cancellationToken).ConfigureAwait(false);
                var previousLabelAssigned = label.IsAssigned;
                label.IsAssigned = true;
                reservedLabelIds.Add(label.Id);

                var versionId = Guid.NewGuid();

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
                    WipLabelId = label.Id,
                };

                await _dbContext.WipReceipts.AddAsync(receipt, cancellationToken).ConfigureAwait(false);

                await _dbContext.ReceiptAudits.AddAsync(
                    new ReceiptAudit
                    {
                        Id = Guid.NewGuid(),
                        VersionId = versionId,
                        ReceiptId = receipt.Id,
                        PartId = item.PartId,
                        SectionId = item.SectionId,
                        OpNumber = item.OpNumber,
                        PreviousQuantity = was,
                        NewQuantity = receipt.Quantity,
                        ReceiptDate = receiptDate,
                        Comment = item.Comment,
                        PreviousBalance = was,
                        NewBalance = balance.Quantity,
                        PreviousLabelId = label.Id,
                        NewLabelId = label.Id,
                        PreviousLabelAssigned = previousLabelAssigned,
                        NewLabelAssigned = true,
                        Action = "Created",
                        UserId = userId,
                        CreatedAt = now,
                    },
                    cancellationToken).ConfigureAwait(false);

                results.Add(new ReceiptItemSummaryDto(
                    item.PartId,
                    item.OpNumber,
                    item.SectionId,
                    item.Quantity,
                    was,
                    balance.Quantity,
                    balance.Id,
                    receipt.Id,
                    label.Id,
                    label.Number,
                    label.IsAssigned,
                    versionId));
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

    private async Task<WipLabel> ResolveLabelAsync(ReceiptItemDto in_item, ISet<Guid> in_reservedLabelIds, CancellationToken cancellationToken)
    {
        if (in_item is null)
        {
            throw new ArgumentNullException(nameof(in_item));
        }

        if (in_reservedLabelIds is null)
        {
            throw new ArgumentNullException(nameof(in_reservedLabelIds));
        }

        WipLabel? ret = null;

        string? normalizedLabelNumber = null;

        if (!string.IsNullOrWhiteSpace(in_item.LabelNumber))
        {
            normalizedLabelNumber = NormalizeLabelNumber(in_item.LabelNumber);
        }

        if (in_item.WipLabelId.HasValue)
        {
            ret = await _dbContext.WipLabels
                .FirstOrDefaultAsync(x => x.Id == in_item.WipLabelId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (ret is null)
            {
                throw new InvalidOperationException($"Ярлык с идентификатором {in_item.WipLabelId.Value} не найден.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(normalizedLabelNumber))
        {
            ret = await _dbContext.WipLabels
                .FirstOrDefaultAsync(x => x.Number == normalizedLabelNumber, cancellationToken)
                .ConfigureAwait(false);

            if (ret is null)
            {
                ret = new WipLabel
                {
                    Id = Guid.NewGuid(),
                    PartId = in_item.PartId,
                    LabelDate = NormalizeLabelDate(in_item.ReceiptDate),
                    Quantity = in_item.Quantity,
                    RemainingQuantity = in_item.Quantity,
                    Number = normalizedLabelNumber,
                    IsAssigned = true,
                };

                await _dbContext.WipLabels.AddAsync(ret, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            ret = await _dbContext.WipLabels
                .Where(x => !x.IsAssigned && x.PartId == in_item.PartId && x.Quantity == in_item.Quantity)
                .Where(x => !in_reservedLabelIds.Contains(x.Id))
                .OrderBy(x => x.Number)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (ret is null)
            {
                throw new InvalidOperationException("Свободный ярлык с подходящим количеством не найден. Создайте новый ярлык или выберите существующий перед сохранением прихода.");
            }
        }

        if (in_reservedLabelIds.Contains(ret.Id))
        {
            throw new InvalidOperationException($"Ярлык {ret.Number} уже зарезервирован в текущей операции сохранения.");
        }

        if (ret.PartId != in_item.PartId)
        {
            throw new InvalidOperationException($"Ярлык {ret.Number} относится к другой детали и не может быть использован для прихода детали {in_item.PartId}.");
        }

        if (ret.Quantity != in_item.Quantity)
        {
            throw new InvalidOperationException($"Количество ярлыка {ret.Number} ({ret.Quantity}) не совпадает с количеством прихода ({in_item.Quantity}).");
        }

        if (ret.IsAssigned && !in_item.IsAssigned)
        {
            throw new InvalidOperationException($"Ярлык {ret.Number} уже назначен и не может быть использован повторно.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedLabelNumber) && !string.Equals(normalizedLabelNumber, ret.Number, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Переданный номер ярлыка {normalizedLabelNumber} не совпадает с фактическим номером {ret.Number}.");
        }

        return ret;
    }

    public async Task<ReceiptRevertResultDto> RevertReceiptAsync(Guid in_receiptId, Guid in_versionId, CancellationToken cancellationToken = default)
    {
        if (in_receiptId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор прихода не задан.", nameof(in_receiptId));
        }

        if (in_versionId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор версии не задан.", nameof(in_versionId));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var audit = await _dbContext.ReceiptAudits
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ReceiptId == in_receiptId && x.VersionId == in_versionId, cancellationToken)
                .ConfigureAwait(false);

            if (audit is null)
            {
                throw new KeyNotFoundException($"Версия {in_versionId} для прихода {in_receiptId} не найдена.");
            }

            if (audit.NewBalance < 0)
            {
                throw new InvalidOperationException("Откат версии приведёт к отрицательному остатку.");
            }

            var balance = await _dbContext.WipBalances
                .FirstOrDefaultAsync(
                    x => x.PartId == audit.PartId && x.SectionId == audit.SectionId && x.OpNumber == audit.OpNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            if (balance is null)
            {
                balance = new WipBalance
                {
                    Id = Guid.NewGuid(),
                    PartId = audit.PartId,
                    SectionId = audit.SectionId,
                    OpNumber = audit.OpNumber,
                    Quantity = 0m,
                };

                await _dbContext.WipBalances.AddAsync(balance, cancellationToken).ConfigureAwait(false);
            }

            var receipt = await _dbContext.WipReceipts
                .FirstOrDefaultAsync(x => x.Id == in_receiptId, cancellationToken)
                .ConfigureAwait(false);

            var label = audit.NewLabelId.HasValue
                ? await _dbContext.WipLabels.FirstOrDefaultAsync(x => x.Id == audit.NewLabelId.Value, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            if (audit.NewLabelId.HasValue && label is null)
            {
                throw new InvalidOperationException("Ярлык, зафиксированный в версии, не найден.");
            }

            var previousBalance = balance.Quantity;
            var previousQuantity = receipt?.Quantity;
            var previousLabelId = receipt?.WipLabelId;
            var previousLabelAssigned = label?.IsAssigned ?? false;
            var versionId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            if (audit.NewQuantity.HasValue)
            {
                if (receipt is null)
                {
                    receipt = new WipReceipt
                    {
                        Id = audit.ReceiptId,
                        UserId = _currentUserService.UserId,
                        PartId = audit.PartId,
                        SectionId = audit.SectionId,
                        OpNumber = audit.OpNumber,
                        ReceiptDate = audit.ReceiptDate,
                        CreatedAt = now,
                        Quantity = audit.NewQuantity.Value,
                        Comment = audit.Comment,
                        WipLabelId = label?.Id,
                    };

                    await _dbContext.WipReceipts.AddAsync(receipt, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    receipt.PartId = audit.PartId;
                    receipt.SectionId = audit.SectionId;
                    receipt.OpNumber = audit.OpNumber;
                    receipt.ReceiptDate = audit.ReceiptDate;
                    receipt.Quantity = audit.NewQuantity.Value;
                    receipt.Comment = audit.Comment;
                    receipt.WipLabelId = label?.Id;
                    receipt.UserId = _currentUserService.UserId;
                }

                if (label is not null)
                {
                    label.IsAssigned = audit.NewLabelAssigned;
                }

                balance.Quantity = audit.NewBalance;
            }
            else
            {
                if (receipt is not null)
                {
                    _dbContext.WipReceipts.Remove(receipt);
                }

                if (label is not null)
                {
                    label.IsAssigned = audit.NewLabelAssigned;
                }

                balance.Quantity = audit.NewBalance;
            }

            await _dbContext.ReceiptAudits.AddAsync(
                new ReceiptAudit
                {
                    Id = Guid.NewGuid(),
                    VersionId = versionId,
                    ReceiptId = audit.ReceiptId,
                    PartId = audit.PartId,
                    SectionId = audit.SectionId,
                    OpNumber = audit.OpNumber,
                    PreviousQuantity = previousQuantity,
                    NewQuantity = audit.NewQuantity,
                    ReceiptDate = audit.ReceiptDate,
                    Comment = audit.Comment,
                    PreviousBalance = previousBalance,
                    NewBalance = audit.NewBalance,
                    PreviousLabelId = previousLabelId,
                    NewLabelId = audit.NewLabelId,
                    PreviousLabelAssigned = previousLabelAssigned,
                    NewLabelAssigned = audit.NewLabelAssigned,
                    Action = "Reverted",
                    UserId = _currentUserService.UserId,
                    CreatedAt = now,
                },
                cancellationToken).ConfigureAwait(false);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ReceiptRevertResultDto(
                audit.ReceiptId,
                balance.Id,
                audit.PartId,
                audit.SectionId,
                audit.OpNumber,
                audit.NewQuantity ?? 0m,
                previousQuantity ?? 0m,
                audit.NewQuantity ?? 0m,
                versionId);
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
                var outgoingTransfers = await _dbContext.TransferAudits
                    .AsNoTracking()
                    .Where(transfer =>
                        transfer.PartId == receipt.PartId &&
                            transfer.FromSectionId == receipt.SectionId &&
                            transfer.FromOpNumber == receipt.OpNumber &&
                            !transfer.IsReverted)
                    .Select(transfer => transfer.Quantity)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var transfersCount = outgoingTransfers.Count;
                var transferredQuantity = outgoingTransfers.Sum();

                var message = transfersCount > 0
                    ? $"Нельзя удалить приход: по нему проведено {transfersCount} передач на {transferredQuantity:0.###} шт. Сначала отмените передачи."
                    : "Отмена прихода приведёт к отрицательному остатку.";

                throw new InvalidOperationException(message);
            }

            balance.Quantity = restoredQuantity;

            var label = receipt.WipLabelId.HasValue
                ? await _dbContext.WipLabels.FirstOrDefaultAsync(x => x.Id == receipt.WipLabelId.Value, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            var previousLabelAssigned = label?.IsAssigned ?? false;
            if (label is not null)
            {
                label.IsAssigned = false;
            }

            var versionId = Guid.NewGuid();
            var now = DateTime.UtcNow;

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
                CreatedAt = now,
            };

            _dbContext.WipBalanceAdjustments.Add(adjustment);
            await _dbContext.ReceiptAudits.AddAsync(
                new ReceiptAudit
                {
                    Id = Guid.NewGuid(),
                    VersionId = versionId,
                    ReceiptId = receipt.Id,
                    PartId = receipt.PartId,
                    SectionId = receipt.SectionId,
                    OpNumber = receipt.OpNumber,
                    PreviousQuantity = receipt.Quantity,
                    NewQuantity = null,
                    ReceiptDate = receipt.ReceiptDate,
                    Comment = receipt.Comment,
                    PreviousBalance = previousQuantity,
                    NewBalance = restoredQuantity,
                    PreviousLabelId = label?.Id,
                    NewLabelId = label?.Id,
                    PreviousLabelAssigned = previousLabelAssigned,
                    NewLabelAssigned = label?.IsAssigned ?? false,
                    Action = "Deleted",
                    UserId = _currentUserService.UserId,
                    CreatedAt = now,
                },
                cancellationToken).ConfigureAwait(false);
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
                adjustment.Delta,
                versionId);
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

    private static string NormalizeLabelNumber(string in_labelNumber)
    {
        if (string.IsNullOrWhiteSpace(in_labelNumber))
        {
            throw new InvalidOperationException("Номер ярлыка не может быть пустым.");
        }

        var trimmed = in_labelNumber.Trim();

        if (trimmed.Length > 5)
        {
            throw new InvalidOperationException("Номер ярлыка не может содержать более 5 символов.");
        }

        if (!trimmed.All(char.IsDigit))
        {
            throw new InvalidOperationException("Номер ярлыка должен содержать только цифры.");
        }

        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            throw new InvalidOperationException("Номер ярлыка должен быть положительным числом.");
        }

        var ret = number.ToString("D5", CultureInfo.InvariantCulture);
        return ret;
    }

    private static DateTime NormalizeLabelDate(DateTime in_date)
    {
        var ret = DateTime.SpecifyKind(in_date.Date, DateTimeKind.Utc);
        return ret;
    }
}
