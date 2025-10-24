using System.Linq;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Transfers;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;

namespace UchetNZP.Application.Services;

public class TransferService : ITransferService
{
    private readonly AppDbContext _dbContext;
    private readonly IRouteService _routeService;
    private readonly ICurrentUserService _currentUserService;

    public TransferService(AppDbContext dbContext, IRouteService routeService, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<TransferBatchSummaryDto> AddTransfersBatchAsync(IEnumerable<TransferItemDto> items, CancellationToken cancellationToken = default)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var materialized = items.ToList();
        if (materialized.Count == 0)
        {
            return new TransferBatchSummaryDto(0, Array.Empty<TransferItemSummaryDto>());
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var userId = _currentUserService.UserId;
            var now = DateTime.UtcNow;
            var routeCache = new Dictionary<Guid, List<PartRoute>>();
            var results = new List<TransferItemSummaryDto>(materialized.Count);

            foreach (var item in materialized)
            {
                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException($"Количество для перехода детали {item.PartId} должно быть больше нуля.");
                }

                if (!routeCache.TryGetValue(item.PartId, out var orderedRoute))
                {
                    var route = await _routeService.GetRouteAsync(item.PartId, cancellationToken).ConfigureAwait(false);
                    if (route.Count == 0)
                    {
                        throw new InvalidOperationException($"Маршрут для детали {item.PartId} не найден.");
                    }

                    orderedRoute = route.ToList();
                    routeCache[item.PartId] = orderedRoute;
                }

                var fromRoute = orderedRoute.FirstOrDefault(x => x.OpNumber == item.FromOpNumber);
                if (fromRoute is null)
                {
                    throw new InvalidOperationException($"Операция {item.FromOpNumber} для детали {item.PartId} отсутствует в маршруте.");
                }

                var isWarehouseTransfer = item.ToOpNumber == WarehouseDefaults.OperationNumber;

                PartRoute? toRoute = null;
                if (!isWarehouseTransfer)
                {
                    toRoute = orderedRoute.FirstOrDefault(x => x.OpNumber == item.ToOpNumber);
                    if (toRoute is null)
                    {
                        throw new InvalidOperationException($"Операция {item.ToOpNumber} для детали {item.PartId} отсутствует в маршруте.");
                    }
                }

                var fromIndex = orderedRoute.FindIndex(x => x.OpNumber == item.FromOpNumber);
                if (fromIndex < 0)
                {
                    throw new InvalidOperationException($"Операция {item.FromOpNumber} для детали {item.PartId} отсутствует в маршруте.");
                }

                if (!isWarehouseTransfer)
                {
                    var toIndex = orderedRoute.FindIndex(x => x.OpNumber == item.ToOpNumber);
                    if (toIndex < 0 || toIndex <= fromIndex)
                    {
                        throw new InvalidOperationException($"Операция {item.ToOpNumber} должна следовать за {item.FromOpNumber} для детали {item.PartId}.");
                    }
                }

                var fromBalance = await _dbContext.WipBalances
                    .FirstOrDefaultAsync(
                        x => x.PartId == item.PartId && x.OpNumber == item.FromOpNumber && x.SectionId == fromRoute.SectionId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (fromBalance is null)
                {
                    throw new InvalidOperationException($"Остаток НЗП для операции {item.FromOpNumber} детали {item.PartId} отсутствует.");
                }

                if (item.Quantity > fromBalance.Quantity)
                {
                    throw new InvalidOperationException($"Недостаточно остатка НЗП на операции {item.FromOpNumber} детали {item.PartId}. Доступно {fromBalance.Quantity}, требуется {item.Quantity}.");
                }

                decimal toBalanceBefore;
                decimal toBalanceAfter;
                Guid toSectionId;
                Guid toOperationId;
                Guid? toPartRouteId = null;

                WipBalance? toBalance = null;
                if (isWarehouseTransfer)
                {
                    var warehouseBefore = await _dbContext.WarehouseItems
                        .Where(x => x.PartId == item.PartId)
                        .SumAsync(x => x.Quantity, cancellationToken)
                        .ConfigureAwait(false);

                    toBalanceBefore = warehouseBefore;
                    toBalanceAfter = warehouseBefore + item.Quantity;
                    toSectionId = WarehouseDefaults.SectionId;
                    toOperationId = WarehouseDefaults.OperationId;
                }
                else
                {
                    toBalance = await _dbContext.WipBalances
                        .FirstOrDefaultAsync(
                            x => x.PartId == item.PartId && x.OpNumber == item.ToOpNumber && x.SectionId == toRoute!.SectionId,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (toBalance is null)
                    {
                        toBalanceBefore = 0m;
                        toBalance = new WipBalance
                        {
                            Id = Guid.NewGuid(),
                            PartId = item.PartId,
                            SectionId = toRoute!.SectionId,
                            OpNumber = item.ToOpNumber,
                            Quantity = 0m,
                        };

                        await _dbContext.WipBalances.AddAsync(toBalance, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        toBalanceBefore = toBalance.Quantity;
                    }

                    toBalance.Quantity = toBalance.Quantity + item.Quantity;
                    toBalanceAfter = toBalance.Quantity;
                    toSectionId = toRoute!.SectionId;
                    toOperationId = toRoute.OperationId;
                    toPartRouteId = toRoute.Id;
                }

                var fromBalanceBefore = fromBalance.Quantity;
                var remainingAfterTransfer = fromBalanceBefore - item.Quantity;
                if (remainingAfterTransfer < 0)
                {
                    throw new InvalidOperationException(
                        $"Остаток НЗП для операции {item.FromOpNumber} детали {item.PartId} не может стать отрицательным.");
                }

                var transferDate = NormalizeToUtc(item.TransferDate);
                var trimmedComment = string.IsNullOrWhiteSpace(item.Comment) ? null : item.Comment.Trim();

                var transfer = new WipTransfer
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PartId = item.PartId,
                    FromSectionId = fromRoute.SectionId,
                    FromOpNumber = item.FromOpNumber,
                    ToSectionId = toSectionId,
                    ToOpNumber = item.ToOpNumber,
                    TransferDate = transferDate,
                    CreatedAt = now,
                    Quantity = item.Quantity,
                    Comment = trimmedComment,
                };

                await _dbContext.WipTransfers.AddAsync(transfer, cancellationToken).ConfigureAwait(false);

                var fromOperation = new WipTransferOperation
                {
                    Id = Guid.NewGuid(),
                    WipTransferId = transfer.Id,
                    OperationId = fromRoute.OperationId,
                    SectionId = fromRoute.SectionId,
                    OpNumber = fromRoute.OpNumber,
                    PartRouteId = fromRoute.Id,
                    QuantityChange = -item.Quantity,
                };

                var toOperation = new WipTransferOperation
                {
                    Id = Guid.NewGuid(),
                    WipTransferId = transfer.Id,
                    OperationId = toOperationId,
                    SectionId = toSectionId,
                    OpNumber = item.ToOpNumber,
                    PartRouteId = toPartRouteId,
                    QuantityChange = item.Quantity,
                };

                await _dbContext.WipTransferOperations.AddRangeAsync(new[] { fromOperation, toOperation }, cancellationToken).ConfigureAwait(false);

                if (isWarehouseTransfer)
                {
                    var warehouseItem = new WarehouseItem
                    {
                        Id = Guid.NewGuid(),
                        PartId = item.PartId,
                        TransferId = transfer.Id,
                        Quantity = item.Quantity,
                        AddedAt = transferDate,
                        CreatedAt = now,
                        UpdatedAt = now,
                        Comment = trimmedComment,
                    };

                    await _dbContext.WarehouseItems.AddAsync(warehouseItem, cancellationToken).ConfigureAwait(false);
                }

                TransferScrapSummaryDto? scrapSummary = null;

                if (item.Scrap is not null)
                {
                    if (item.Scrap.Quantity <= 0)
                    {
                        throw new InvalidOperationException($"Количество брака должно быть положительным для детали {item.PartId}.");
                    }

                    if (remainingAfterTransfer != item.Scrap.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Количество брака ({item.Scrap.Quantity}) не совпадает с остатком ({remainingAfterTransfer}) для операции {item.FromOpNumber} детали {item.PartId}.");
                    }

                    fromBalance.Quantity = 0m;

                    var scrap = new WipScrap
                    {
                        Id = Guid.NewGuid(),
                        PartId = item.PartId,
                        SectionId = fromRoute.SectionId,
                        OpNumber = item.FromOpNumber,
                        Quantity = item.Scrap.Quantity,
                        ScrapType = item.Scrap.ScrapType,
                        RecordedAt = now,
                        UserId = userId,
                        Comment = item.Scrap.Comment,
                        TransferId = transfer.Id,
                    };

                    await _dbContext.WipScraps.AddAsync(scrap, cancellationToken).ConfigureAwait(false);

                    scrapSummary = new TransferScrapSummaryDto(scrap.Id, scrap.ScrapType, scrap.Quantity, scrap.Comment);
                }
                else
                {
                    fromBalance.Quantity = remainingAfterTransfer;
                }

                results.Add(new TransferItemSummaryDto(
                    item.PartId,
                    item.FromOpNumber,
                    fromRoute.SectionId,
                    fromBalanceBefore,
                    fromBalance.Quantity,
                    item.ToOpNumber,
                    toSectionId,
                    toBalanceBefore,
                    isWarehouseTransfer ? toBalanceAfter : toBalance!.Quantity,
                    item.Quantity,
                    transfer.Id,
                    scrapSummary));
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new TransferBatchSummaryDto(results.Count, results);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<TransferDeleteResultDto> DeleteTransferAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        if (transferId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор передачи не может быть пустым.", nameof(transferId));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var transfer = await _dbContext.WipTransfers
                .Include(x => x.Operations)
                .Include(x => x.Scrap)
                .Include(x => x.WarehouseItem)
                .FirstOrDefaultAsync(x => x.Id == transferId, cancellationToken)
                .ConfigureAwait(false);

            if (transfer is null)
            {
                throw new KeyNotFoundException($"Передача {transferId} не найдена.");
            }

            var operations = transfer.Operations.ToList();
            if (operations.Count == 0)
            {
                throw new InvalidOperationException($"Для передачи {transferId} не найдены связанные операции.");
            }

            var fromOperation = operations.FirstOrDefault(x => x.QuantityChange < 0);
            var toOperation = operations.FirstOrDefault(x => x.QuantityChange > 0);

            if (fromOperation is null || toOperation is null)
            {
                throw new InvalidOperationException($"Передача {transferId} имеет некорректный набор операций.");
            }

            var fromBalance = await _dbContext.WipBalances
                .FirstOrDefaultAsync(
                    x => x.PartId == transfer.PartId &&
                         x.SectionId == transfer.FromSectionId &&
                         x.OpNumber == transfer.FromOpNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            if (fromBalance is null)
            {
                throw new InvalidOperationException(
                    $"Остаток НЗП для операции {transfer.FromOpNumber} детали {transfer.PartId} не найден.");
            }

            var fromBalanceBefore = fromBalance.Quantity;
            var scrap = transfer.Scrap;
            var scrapQuantity = scrap?.Quantity ?? 0m;
            var fromBalanceAfter = fromBalanceBefore + transfer.Quantity + scrapQuantity;
            fromBalance.Quantity = fromBalanceAfter;

            TransferDeleteWarehouseItemDto? warehouseDto = null;
            decimal toBalanceBefore;
            decimal toBalanceAfter;
            var isWarehouseTransfer = transfer.ToOpNumber == WarehouseDefaults.OperationNumber;

            if (isWarehouseTransfer)
            {
                var warehouseItem = transfer.WarehouseItem;
                if (warehouseItem is null)
                {
                    warehouseItem = await _dbContext.WarehouseItems
                        .FirstOrDefaultAsync(x => x.TransferId == transfer.Id, cancellationToken)
                        .ConfigureAwait(false);

                    if (warehouseItem is null)
                    {
                        throw new InvalidOperationException(
                            $"Для передачи {transferId} не найдена запись на складе.");
                    }
                }

                toBalanceBefore = await _dbContext.WarehouseItems
                    .Where(x => x.PartId == transfer.PartId)
                    .SumAsync(x => x.Quantity, cancellationToken)
                    .ConfigureAwait(false);

                toBalanceAfter = toBalanceBefore - warehouseItem.Quantity;
                if (toBalanceAfter < -0.000001m)
                {
                    throw new InvalidOperationException(
                        "Отмена передачи приведёт к отрицательному остатку на складе.");
                }

                warehouseDto = new TransferDeleteWarehouseItemDto(warehouseItem.Id, warehouseItem.Quantity);
                _dbContext.WarehouseItems.Remove(warehouseItem);
            }
            else
            {
                var toBalance = await _dbContext.WipBalances
                    .FirstOrDefaultAsync(
                        x => x.PartId == transfer.PartId &&
                             x.SectionId == transfer.ToSectionId &&
                             x.OpNumber == transfer.ToOpNumber,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (toBalance is null)
                {
                    throw new InvalidOperationException(
                        $"Остаток НЗП для операции {transfer.ToOpNumber} детали {transfer.PartId} не найден.");
                }

                toBalanceBefore = toBalance.Quantity;
                toBalanceAfter = toBalanceBefore - transfer.Quantity;

                if (toBalanceAfter < -0.000001m)
                {
                    throw new InvalidOperationException(
                        "Отмена передачи приведёт к отрицательному остатку на операции после.");
                }

                toBalance.Quantity = toBalanceAfter;
            }

            TransferDeleteScrapDto? scrapDto = null;
            if (scrap is not null)
            {
                scrapDto = new TransferDeleteScrapDto(scrap.Id, scrap.ScrapType, scrap.Quantity, scrap.Comment);
                _dbContext.WipScraps.Remove(scrap);
            }

            if (operations.Count > 0)
            {
                _dbContext.WipTransferOperations.RemoveRange(operations);
            }

            _dbContext.WipTransfers.Remove(transfer);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new TransferDeleteResultDto(
                transfer.Id,
                transfer.PartId,
                transfer.FromOpNumber,
                transfer.FromSectionId,
                fromBalanceBefore,
                fromBalanceAfter,
                transfer.ToOpNumber,
                transfer.ToSectionId,
                toBalanceBefore,
                toBalanceAfter,
                transfer.Quantity,
                isWarehouseTransfer,
                operations.Select(x => x.Id).ToArray(),
                scrapDto,
                warehouseDto);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
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
