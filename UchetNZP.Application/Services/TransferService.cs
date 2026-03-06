using System.Data;
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
    private readonly ILabelNumberingService _labelNumberingService;

    public TransferService(AppDbContext dbContext, IRouteService routeService, ICurrentUserService currentUserService, ILabelNumberingService labelNumberingService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _labelNumberingService = labelNumberingService ?? throw new ArgumentNullException(nameof(labelNumberingService));
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

        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await AddTransfersBatchCoreAsync(materialized, cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (attempt < maxRetries && IsLabelNumberUniqueConflict(ex))
            {
                foreach (var entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        throw new InvalidOperationException("Не удалось выполнить пакет перемещений из-за конфликта уникальности номера ярлыка.");
    }

    private async Task<TransferBatchSummaryDto> AddTransfersBatchCoreAsync(List<TransferItemDto> materialized, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        try
        {
            var userId = _currentUserService.UserId;
            var now = DateTime.UtcNow;
            var routeCache = new Dictionary<Guid, List<PartRoute>>();
            var results = new List<TransferItemSummaryDto>(materialized.Count);
            var transactionId = Guid.NewGuid();
            var balanceCache = new Dictionary<(Guid PartId, Guid SectionId, int OpNumber), WipBalance>();
            var ledgerEvents = new List<WipLabelLedger>();
            var transferLabelUsages = new List<TransferLabelUsage>();

            async Task<WipBalance?> GetExistingBalanceAsync(Guid partId, Guid sectionId, int opNumber)
            {
                var key = (partId, sectionId, opNumber);
                if (balanceCache.TryGetValue(key, out var cachedBalance))
                {
                    return cachedBalance;
                }

                var trackedBalance = _dbContext.WipBalances.Local
                    .FirstOrDefault(x => x.PartId == partId && x.SectionId == sectionId && x.OpNumber == opNumber);
                if (trackedBalance is not null)
                {
                    balanceCache[key] = trackedBalance;
                    return trackedBalance;
                }

                var balance = await _dbContext.WipBalances
                    .FirstOrDefaultAsync(
                        x => x.PartId == partId && x.OpNumber == opNumber && x.SectionId == sectionId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (balance is not null)
                {
                    balanceCache[key] = balance;
                }

                return balance;
            }

            async Task<WipBalance> GetOrCreateBalanceAsync(Guid partId, Guid sectionId, int opNumber)
            {
                var existing = await GetExistingBalanceAsync(partId, sectionId, opNumber).ConfigureAwait(false);
                if (existing is not null)
                {
                    return existing;
                }

                var created = new WipBalance
                {
                    Id = Guid.NewGuid(),
                    PartId = partId,
                    SectionId = sectionId,
                    OpNumber = opNumber,
                    Quantity = 0m,
                };

                await _dbContext.WipBalances.AddAsync(created, cancellationToken).ConfigureAwait(false);
                balanceCache[(partId, sectionId, opNumber)] = created;
                return created;
            }

            foreach (var item in materialized)
            {
                WipLabelInvariants.EnsurePositiveTransferQuantity(item.Quantity);

                var scenario = item.Scenario;
                var isMoveLabel = scenario == TransferScenario.MoveLabel;
                var isSplitAndTransfer = scenario == TransferScenario.SplitAndTransfer;
                var createsTransferChildLabel = scenario == TransferScenario.TransferFromLabel || isSplitAndTransfer;

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

                var isWarehouseTransfer = WarehouseDefaults.IsWarehouseOperationNumber(item.ToOpNumber);
                var normalizedToOpNumber = isWarehouseTransfer ? WarehouseDefaults.OperationNumber : item.ToOpNumber;

                if (isWarehouseTransfer)
                {
                    await EnsureWarehouseReferencesAsync(cancellationToken).ConfigureAwait(false);
                }

                PartRoute? toRoute = null;
                if (!isWarehouseTransfer)
                {
                    toRoute = orderedRoute.FirstOrDefault(x => x.OpNumber == normalizedToOpNumber);
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
                    var toIndex = orderedRoute.FindIndex(x => x.OpNumber == normalizedToOpNumber);
                    if (toIndex < 0 || toIndex <= fromIndex)
                    {
                        throw new InvalidOperationException($"Операция {item.ToOpNumber} должна следовать за {item.FromOpNumber} для детали {item.PartId}.");
                    }
                }

                var fromBalance = await GetExistingBalanceAsync(item.PartId, fromRoute.SectionId, item.FromOpNumber)
                    .ConfigureAwait(false);

                if (fromBalance is null)
                {
                    throw new InvalidOperationException($"Остаток НЗП для операции {item.FromOpNumber} детали {item.PartId} отсутствует.");
                }

                WipLabelInvariants.EnsureCanTransferFromBalance(
                    fromBalance.Quantity,
                    item.Quantity,
                    $"операции {item.FromOpNumber} детали {item.PartId}");

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
                    toBalance = await GetOrCreateBalanceAsync(item.PartId, toRoute!.SectionId, item.ToOpNumber)
                        .ConfigureAwait(false);

                    toBalanceBefore = toBalance.Quantity;

                    toBalance.Quantity = toBalance.Quantity + item.Quantity;
                    toBalanceAfter = toBalance.Quantity;
                    toSectionId = toRoute!.SectionId;
                    toOperationId = toRoute.OperationId;
                    toPartRouteId = toRoute.Id;
                }

                var fromBalanceBefore = fromBalance.Quantity;
                var remainingAfterTransfer = fromBalanceBefore - item.Quantity;

                var transferDate = NormalizeToUtc(item.TransferDate);
                var trimmedComment = string.IsNullOrWhiteSpace(item.Comment) ? null : item.Comment.Trim();

                Guid? transferLabelId = null;
                string? transferLabelNumber = null;
                decimal? labelQuantityBefore = null;
                decimal? labelQuantityAfter = null;

                var transfer = new WipTransfer
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PartId = item.PartId,
                    FromSectionId = fromRoute.SectionId,
                    FromOpNumber = item.FromOpNumber,
                    ToSectionId = toSectionId,
                    ToOpNumber = normalizedToOpNumber,
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
                    OpNumber = normalizedToOpNumber,
                    PartRouteId = toPartRouteId,
                    QuantityChange = item.Quantity,
                };

                await _dbContext.WipTransferOperations.AddRangeAsync(new[] { fromOperation, toOperation }, cancellationToken).ConfigureAwait(false);

                WarehouseItem? warehouseItem = null;

                if (isWarehouseTransfer)
                {
                    warehouseItem = new WarehouseItem
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
                var scrapQuantity = 0m;

                if (item.Scrap is not null)
                {
                    if (item.Scrap.Quantity <= 0)
                    {
                        throw new InvalidOperationException($"Количество брака должно быть положительным для детали {item.PartId}.");
                    }

                    if (item.Scrap.Quantity > remainingAfterTransfer)
                    {
                        throw new InvalidOperationException(
                            $"Количество брака ({item.Scrap.Quantity}) не может превышать доступный остаток ({remainingAfterTransfer}) для операции {item.FromOpNumber} детали {item.PartId}.");
                    }

                    scrapQuantity = item.Scrap.Quantity;

                    fromBalance.Quantity = remainingAfterTransfer - item.Scrap.Quantity;

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

                var labelUsage = await ResolveTransferLabelAsync(
                        item,
                        fromRoute,
                        isWarehouseTransfer,
                        isMoveLabel,
                        scrapQuantity,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (createsTransferChildLabel && labelUsage is null)
                {
                    throw new InvalidOperationException("Для выбранного сценария требуется исходный ярлык на операции передачи.");
                }

                Guid? residualLabelId = null;
                string? residualLabelNumber = null;
                decimal? residualLabelQuantity = null;

                if (labelUsage is not null)
                {
                    var residualLabel = await CreateResidualLabelAsync(
                            item,
                            labelUsage.Label,
                            createsTransferChildLabel ? item.Quantity : 0m,
                            isWarehouseTransfer ? WarehouseDefaults.SectionId : toSectionId,
                            normalizedToOpNumber,
                            transferDate,
                            cancellationToken)
                        .ConfigureAwait(false);

                    var transferLabel = residualLabel ?? labelUsage.Label;
                    transferLabelId = transferLabel.Id;
                    transfer.WipLabelId = transferLabelId;

                    if (residualLabel is not null)
                    {
                        residualLabelId = residualLabel.Id;
                        residualLabelNumber = residualLabel.Number;
                        residualLabelQuantity = residualLabel.Quantity;
                    }

                    transferLabelNumber = transferLabel.Number;
                    labelQuantityBefore = labelUsage.RemainingBefore;
                    labelQuantityAfter = labelUsage.RemainingAfter;

                    if (isWarehouseTransfer && warehouseItem is not null)
                    {
                        var warehouseLabelItem = new WarehouseLabelItem
                        {
                            Id = Guid.NewGuid(),
                            WarehouseItemId = warehouseItem.Id,
                            WipLabelId = transferLabel.Id,
                            Quantity = item.Quantity,
                            AddedAt = transferDate,
                            UpdatedAt = now,
                        };

                        await _dbContext.WarehouseLabelItems
                            .AddAsync(warehouseLabelItem, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (!isWarehouseTransfer)
                    {
                        transferLabel.CurrentSectionId = toSectionId;
                        transferLabel.CurrentOpNumber = item.ToOpNumber;
                        if (transferLabel.Status == WipLabelStatus.Consumed)
                        {
                            transferLabel.Status = WipLabelStatus.Closed;
                        }
                    }
                    else
                    {
                        transferLabel.CurrentSectionId = WarehouseDefaults.SectionId;
                        transferLabel.CurrentOpNumber = WarehouseDefaults.OperationNumber;
                    }

                    ledgerEvents.Add(new WipLabelLedger
                    {
                        EventId = Guid.NewGuid(),
                        EventTime = now,
                        UserId = userId,
                        TransactionId = transactionId,
                        EventType = isMoveLabel ? WipLabelEventType.Move : WipLabelEventType.Transfer,
                        FromLabelId = labelUsage.Label.Id,
                        ToLabelId = transferLabel.Id,
                        FromSectionId = fromRoute.SectionId,
                        FromOpNumber = item.FromOpNumber,
                        ToSectionId = isWarehouseTransfer ? WarehouseDefaults.SectionId : toSectionId,
                        ToOpNumber = item.ToOpNumber,
                        Qty = item.Quantity,
                        ScrapQty = scrapQuantity,
                        RefEntityType = nameof(WipTransfer),
                        RefEntityId = transfer.Id,
                    });

                    if (scrapQuantity > 0m)
                    {
                        ledgerEvents.Add(new WipLabelLedger
                        {
                            EventId = Guid.NewGuid(),
                            EventTime = now,
                            UserId = userId,
                            TransactionId = transactionId,
                            EventType = WipLabelEventType.Scrap,
                            FromLabelId = labelUsage.Label.Id,
                            ToLabelId = labelUsage.Label.Id,
                            FromSectionId = fromRoute.SectionId,
                            FromOpNumber = item.FromOpNumber,
                            ToSectionId = fromRoute.SectionId,
                            ToOpNumber = item.FromOpNumber,
                            Qty = 0m,
                            ScrapQty = scrapQuantity,
                            RefEntityType = nameof(WipTransfer),
                            RefEntityId = transfer.Id,
                        });
                    }

                    if (residualLabelId.HasValue && isSplitAndTransfer)
                    {
                        ledgerEvents.Add(new WipLabelLedger
                        {
                            EventId = Guid.NewGuid(),
                            EventTime = now,
                            UserId = userId,
                            TransactionId = transactionId,
                            EventType = WipLabelEventType.Split,
                            FromLabelId = labelUsage.Label.Id,
                            ToLabelId = residualLabelId,
                            FromSectionId = fromRoute.SectionId,
                            FromOpNumber = item.FromOpNumber,
                            ToSectionId = fromRoute.SectionId,
                            ToOpNumber = item.FromOpNumber,
                            Qty = residualLabelQuantity ?? 0m,
                            ScrapQty = 0m,
                            RefEntityType = nameof(WipTransfer),
                            RefEntityId = transfer.Id,
                        });
                    }

                    transferLabelUsages.Add(new TransferLabelUsage
                    {
                        Id = Guid.NewGuid(),
                        TransferId = transfer.Id,
                        FromLabelId = labelUsage.Label.Id,
                        Qty = item.Quantity,
                        ScrapQty = scrapQuantity,
                        RemainingBefore = labelUsage.RemainingBefore,
                        CreatedToLabelId = transferLabel.Id != labelUsage.Label.Id ? transferLabel.Id : null,
                    });
                }

                var audit = new TransferAudit
                {
                    Id = Guid.NewGuid(),
                    TransactionId = transactionId,
                    TransferId = transfer.Id,
                    PartId = item.PartId,
                    FromSectionId = fromRoute.SectionId,
                    FromOpNumber = item.FromOpNumber,
                    ToSectionId = toSectionId,
                    ToOpNumber = normalizedToOpNumber,
                    Quantity = item.Quantity,
                    Comment = trimmedComment,
                    TransferDate = transferDate,
                    CreatedAt = now,
                    UserId = userId,
                    FromBalanceBefore = fromBalanceBefore,
                    FromBalanceAfter = fromBalance.Quantity,
                    ToBalanceBefore = toBalanceBefore,
                    ToBalanceAfter = toBalanceAfter,
                    IsWarehouseTransfer = isWarehouseTransfer,
                    WipLabelId = transferLabelId,
                    LabelNumber = transferLabelNumber,
                    LabelQuantityBefore = labelQuantityBefore,
                    LabelQuantityAfter = labelQuantityAfter,
                    ResidualWipLabelId = residualLabelId,
                    ResidualLabelNumber = residualLabelNumber,
                    ResidualLabelQuantity = residualLabelQuantity,
                    ScrapQuantity = scrapQuantity,
                    ScrapType = item.Scrap?.ScrapType,
                    ScrapComment = item.Scrap?.Comment,
                };

                audit.Operations.Add(new TransferAuditOperation
                {
                    Id = Guid.NewGuid(),
                    TransferAuditId = audit.Id,
                    SectionId = fromRoute.SectionId,
                    OpNumber = fromRoute.OpNumber,
                    OperationId = fromRoute.OperationId,
                    PartRouteId = fromRoute.Id,
                    BalanceBefore = fromBalanceBefore,
                    BalanceAfter = fromBalance.Quantity,
                    QuantityChange = -item.Quantity - scrapQuantity,
                    IsWarehouse = false,
                });

                audit.Operations.Add(new TransferAuditOperation
                {
                    Id = Guid.NewGuid(),
                    TransferAuditId = audit.Id,
                    SectionId = toSectionId,
                    OpNumber = item.ToOpNumber,
                    OperationId = toOperationId,
                    PartRouteId = toPartRouteId,
                    BalanceBefore = toBalanceBefore,
                    BalanceAfter = toBalanceAfter,
                    QuantityChange = item.Quantity,
                    IsWarehouse = isWarehouseTransfer,
                });

                await _dbContext.TransferAudits.AddAsync(audit, cancellationToken).ConfigureAwait(false);

                results.Add(new TransferItemSummaryDto(
                    item.PartId,
                    item.FromOpNumber,
                    fromRoute.SectionId,
                    fromBalanceBefore,
                    fromBalance.Quantity,
                    normalizedToOpNumber,
                    toSectionId,
                    toBalanceBefore,
                    isWarehouseTransfer ? toBalanceAfter : toBalance!.Quantity,
                    item.Quantity,
                    transfer.Id,
                    audit.Id,
                    transactionId,
                    scrapSummary,
                    transferLabelId,
                    transferLabelNumber,
                    labelQuantityBefore,
                    labelQuantityAfter,
                    residualLabelNumber,
                    false));
            }

            if (ledgerEvents.Count > 0)
            {
                await _dbContext.WipLabelLedger.AddRangeAsync(ledgerEvents, cancellationToken).ConfigureAwait(false);
            }

            if (transferLabelUsages.Count > 0)
            {
                await _dbContext.TransferLabelUsages.AddRangeAsync(transferLabelUsages, cancellationToken).ConfigureAwait(false);
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

    private async Task EnsureWarehouseReferencesAsync(CancellationToken cancellationToken)
    {
        var hasWarehouseOperation = await _dbContext.Operations
            .AnyAsync(x => x.Id == WarehouseDefaults.OperationId, cancellationToken)
            .ConfigureAwait(false);

        if (!hasWarehouseOperation)
        {
            await _dbContext.Operations.AddAsync(new Operation
            {
                Id = WarehouseDefaults.OperationId,
                Name = WarehouseDefaults.OperationName,
            }, cancellationToken).ConfigureAwait(false);
        }

        var hasWarehouseSection = await _dbContext.Sections
            .AnyAsync(x => x.Id == WarehouseDefaults.SectionId, cancellationToken)
            .ConfigureAwait(false);

        if (!hasWarehouseSection)
        {
            await _dbContext.Sections.AddAsync(new Section
            {
                Id = WarehouseDefaults.SectionId,
                Name = WarehouseDefaults.SectionName,
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsLabelNumberUniqueConflict(DbUpdateException in_exception)
    {
        var message = in_exception.InnerException?.Message ?? in_exception.Message;
        return message.Contains("IX_WipLabels_Number", StringComparison.Ordinal)
            || message.Contains("IX_WipLabels_RootNumber_Suffix", StringComparison.Ordinal);
    }


    private async Task<ResolvedTransferLabelUsage?> ResolveTransferLabelAsync(
        TransferItemDto item,
        PartRoute fromRoute,
        bool isWarehouseTransfer,
        bool isMoveLabel,
        decimal scrapQuantity,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (fromRoute is null)
        {
            throw new ArgumentNullException(nameof(fromRoute));
        }

        var requiredQuantity = item.Quantity + scrapQuantity;
        if (requiredQuantity <= 0m)
        {
            return null;
        }

        var consumesTransferQuantity = !isMoveLabel;
        var consumedFromLabel = scrapQuantity + (consumesTransferQuantity ? item.Quantity : 0m);

        WipLabel? label;

        if (item.WipLabelId.HasValue)
        {
            label = await _dbContext.WipLabels
                .Include(x => x.WipReceipt)
                .FirstOrDefaultAsync(x => x.Id == item.WipLabelId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (label is null)
            {
                throw new InvalidOperationException($"Ярлык с идентификатором {item.WipLabelId.Value} не найден.");
            }
        }
        else
        {
            var candidates = await _dbContext.WipLabels
                .Include(x => x.WipReceipt)
                .Where(x =>
                    x.PartId == item.PartId &&
                    x.WipReceipt != null &&
                    x.WipReceipt.SectionId == fromRoute.SectionId &&
                    x.WipReceipt.OpNumber == item.FromOpNumber)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            label = null;

            foreach (var candidate in candidates)
            {
                var candidateOperationQuantity = await GetLabelQuantityAtOperationAsync(
                        candidate.Id,
                        item.PartId,
                        fromRoute.SectionId,
                        item.FromOpNumber,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (candidateOperationQuantity + 0.000001m < requiredQuantity)
                {
                    continue;
                }

                label = candidate;
                break;
            }

            if (label is null)
            {
                return null;
            }
        }

        if (label is null)
        {
            return null;
        }

        if (label.PartId != item.PartId)
        {
            throw new InvalidOperationException($"Ярлык {label.Number} относится к другой детали и не может быть использован для передачи детали {item.PartId}.");
        }

        if (!isMoveLabel && label.WipReceipt is null)
        {
            throw new InvalidOperationException($"Невозможно выполнить передачу: ярлык {label.Number} не связан с приходом. Используйте сценарий операции с ярлыком, созданным через приход, или выберите другой ярлык.");
        }

        var operationQuantity = isMoveLabel && label.WipReceipt is null && label.CurrentSectionId == fromRoute.SectionId && label.CurrentOpNumber == item.FromOpNumber
            ? label.RemainingQuantity
            : await GetLabelQuantityAtOperationAsync(
                    label.Id,
                    item.PartId,
                    fromRoute.SectionId,
                    item.FromOpNumber,
                    cancellationToken)
                .ConfigureAwait(false);

        if (operationQuantity + 0.000001m < requiredQuantity)
        {
            throw new InvalidOperationException($"Остаток ярлыка {label.Number} на операции {item.FromOpNumber} ({operationQuantity}) меньше требуемого количества ({requiredQuantity}).");
        }

        var remainingBefore = label.RemainingQuantity;
        var remainingAfter = WipLabelInvariants.ConsumeLabelQuantity(
            label.Quantity,
            remainingBefore,
            consumedFromLabel,
            label.Number);

        label.RemainingQuantity = remainingAfter;
        label.Status = WipLabelInvariants.GetStatusAfterConsume(remainingAfter);

        var ret = new ResolvedTransferLabelUsage(label, remainingBefore, remainingAfter);
        return ret;
    }

    private async Task<WipLabel?> CreateResidualLabelAsync(
        TransferItemDto item,
        WipLabel transferLabel,
        decimal transferQuantity,
        Guid toSectionId,
        int toOpNumber,
        DateTime transferDate,
        CancellationToken cancellationToken)
    {
        if (transferQuantity <= 0m)
        {
            return null;
        }

        WipLabelInvariants.EnsureCanTearOff(transferQuantity);

        var currentTransferNumber = transferLabel.Number?.Trim() ?? string.Empty;
        if (transferLabel.RootLabelId == Guid.Empty)
        {
            transferLabel.RootLabelId = transferLabel.Id;
        }

        var parsedTransferNumber = WipLabelInvariants.ParseNumber(currentTransferNumber);
        if (string.IsNullOrWhiteSpace(transferLabel.RootNumber))
        {
            transferLabel.RootNumber = parsedTransferNumber.RootNumber;
        }

        if (transferLabel.Suffix == 0 && parsedTransferNumber.Suffix > 0)
        {
            transferLabel.Suffix = parsedTransferNumber.Suffix;
        }
        var baseNumber = item.ResidualLabelNumber.HasValue
            ? item.ResidualLabelNumber.Value.ToString()
            : transferLabel.RootNumber;

        if (string.IsNullOrWhiteSpace(baseNumber))
        {
            throw new InvalidOperationException("Не удалось определить базовый номер для отрыва ярлыка.");
        }

        var nextIndex = await _labelNumberingService
            .GetNextSuffixAsync(baseNumber, cancellationToken)
            .ConfigureAwait(false);

        var residualNumber = WipLabelInvariants.FormatNumber(baseNumber, nextIndex);
        var duplicateExists = await _dbContext.WipLabels
            .AnyAsync(x => x.RootNumber == baseNumber && x.Suffix == nextIndex, cancellationToken)
            .ConfigureAwait(false);

        var duplicateInMemory = _dbContext.WipLabels.Local
            .Any(x => string.Equals(x.RootNumber, baseNumber, StringComparison.Ordinal) && x.Suffix == nextIndex);

        if (duplicateExists || duplicateInMemory)
        {
            throw new InvalidOperationException($"Ярлык с номером {residualNumber} уже существует.");
        }

        var residualLabel = new WipLabel
        {
            Id = Guid.NewGuid(),
            PartId = item.PartId,
            LabelDate = transferDate,
            LabelYear = transferDate.Year,
            Quantity = transferQuantity,
            RemainingQuantity = transferQuantity,
            Number = residualNumber,
            IsAssigned = true,
            Status = WipLabelStatus.Active,
            CurrentSectionId = toSectionId,
            CurrentOpNumber = toOpNumber,
            RootLabelId = transferLabel.RootLabelId == Guid.Empty ? transferLabel.Id : transferLabel.RootLabelId,
            ParentLabelId = transferLabel.Id,
            RootNumber = baseNumber,
            Suffix = nextIndex,
        };

        await _dbContext.WipLabels.AddAsync(residualLabel, cancellationToken).ConfigureAwait(false);

        return residualLabel;
    }

    private async Task<decimal> GetLabelQuantityAtOperationAsync(
        Guid labelId,
        Guid partId,
        Guid sectionId,
        int opNumber,
        CancellationToken cancellationToken)
    {
        var receiptQuantity = await _dbContext.WipReceipts
            .Where(x =>
                x.WipLabelId == labelId &&
                x.PartId == partId &&
                x.SectionId == sectionId &&
                x.OpNumber == opNumber)
            .SumAsync(x => x.Quantity, cancellationToken)
            .ConfigureAwait(false);

        var incomingTransfers = await _dbContext.TransferAudits
            .Where(x =>
                !x.IsReverted &&
                x.WipLabelId == labelId &&
                x.PartId == partId &&
                !x.IsWarehouseTransfer &&
                x.ToSectionId == sectionId &&
                x.ToOpNumber == opNumber)
            .SumAsync(x => x.Quantity, cancellationToken)
            .ConfigureAwait(false);

        var outgoingTransfers = await _dbContext.TransferAudits
            .Where(x =>
                !x.IsReverted &&
                x.WipLabelId == labelId &&
                x.PartId == partId &&
                x.FromSectionId == sectionId &&
                x.FromOpNumber == opNumber)
            .SumAsync(x => x.Quantity + x.ScrapQuantity + (x.ResidualLabelQuantity ?? 0m), cancellationToken)
            .ConfigureAwait(false);

        var residualIncoming = await _dbContext.TransferAudits
            .Where(x =>
                !x.IsReverted &&
                x.ResidualWipLabelId == labelId &&
                x.PartId == partId &&
                x.FromSectionId == sectionId &&
                x.FromOpNumber == opNumber)
            .SumAsync(x => x.ResidualLabelQuantity ?? 0m, cancellationToken)
            .ConfigureAwait(false);

        return receiptQuantity + incomingTransfers + residualIncoming - outgoingTransfers;
    }

    public async Task<TransferDeleteResultDto> DeleteTransferAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        if (transferId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор передачи не может быть пустым.", nameof(transferId));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        try
        {
            var now = DateTime.UtcNow;
            var userId = _currentUserService.UserId;
            var deleteTransactionId = Guid.NewGuid();

            var transfer = await _dbContext.WipTransfers
                .Include(x => x.Operations)
                .Include(x => x.Scrap)
                .Include(x => x.WarehouseItem)
                .Include(x => x.WipLabel)
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

            Guid? transferLabelId = transfer.WipLabelId;
            string? transferLabelNumber = transfer.WipLabel?.Number;
            decimal? labelQuantityBefore = null;
            decimal? labelQuantityAfter = null;

            TransferDeleteWarehouseItemDto? warehouseDto = null;
            decimal toBalanceBefore;
            decimal toBalanceAfter;
            var isWarehouseTransfer = WarehouseDefaults.IsWarehouseOperationNumber(transfer.ToOpNumber);

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

            if (transferLabelId.HasValue)
            {
                var label = transfer.WipLabel ?? await _dbContext.WipLabels
                    .FirstOrDefaultAsync(x => x.Id == transferLabelId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (label is null)
                {
                    throw new InvalidOperationException($"Для передачи {transferId} не найден связанный ярлык {transferLabelId.Value}.");
                }

                labelQuantityBefore = label.RemainingQuantity;
                var restoreQuantity = scrapQuantity + (isWarehouseTransfer ? transfer.Quantity : 0m);
                var updatedQuantity = label.RemainingQuantity + restoreQuantity;
                if (updatedQuantity > label.Quantity)
                {
                    updatedQuantity = label.Quantity;
                }

                label.RemainingQuantity = updatedQuantity;
                label.Status = WipLabelInvariants.GetStatusAfterConsume(updatedQuantity);
                label.CurrentSectionId = transfer.FromSectionId;
                label.CurrentOpNumber = transfer.FromOpNumber;

                await _dbContext.WipLabelLedger.AddAsync(new WipLabelLedger
                {
                    EventId = Guid.NewGuid(),
                    EventTime = now,
                    UserId = userId,
                    TransactionId = deleteTransactionId,
                    EventType = WipLabelEventType.Revert,
                    FromLabelId = label.Id,
                    ToLabelId = label.Id,
                    FromSectionId = transfer.ToSectionId,
                    FromOpNumber = transfer.ToOpNumber,
                    ToSectionId = transfer.FromSectionId,
                    ToOpNumber = transfer.FromOpNumber,
                    Qty = transfer.Quantity,
                    ScrapQty = scrapQuantity,
                    RefEntityType = nameof(WipTransfer),
                    RefEntityId = transfer.Id,
                }, cancellationToken).ConfigureAwait(false);
                labelQuantityAfter = updatedQuantity;
                transferLabelNumber = label.Number;
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
                warehouseDto,
                transferLabelId,
                transferLabelNumber,
                labelQuantityBefore,
                labelQuantityAfter);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<TransferDeleteResultDto> RevertTransferAsync(Guid transferAuditId, CancellationToken cancellationToken = default)
    {
        if (transferAuditId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор аудита передачи не может быть пустым.", nameof(transferAuditId));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        try
        {
            var now = DateTime.UtcNow;
            var userId = _currentUserService.UserId;
            var revertTransactionId = Guid.NewGuid();

            var audit = await _dbContext.TransferAudits
                .Include(x => x.Operations)
                .FirstOrDefaultAsync(x => x.Id == transferAuditId, cancellationToken)
                .ConfigureAwait(false);

            if (audit is null)
            {
                throw new KeyNotFoundException($"Аудит передачи {transferAuditId} не найден.");
            }

            if (audit.IsReverted)
            {
                throw new InvalidOperationException($"Аудит передачи {transferAuditId} уже использован для отката.");
            }

            var transfer = await _dbContext.WipTransfers
                .Include(x => x.Operations)
                .Include(x => x.Scrap)
                .Include(x => x.WarehouseItem)
                .Include(x => x.WipLabel)
                .FirstOrDefaultAsync(x => x.Id == audit.TransferId, cancellationToken)
                .ConfigureAwait(false);

            if (transfer is null)
            {
                throw new KeyNotFoundException($"Передача {audit.TransferId} не найдена для отката.");
            }

            var operations = transfer.Operations.ToList();
            if (operations.Count == 0)
            {
                throw new InvalidOperationException($"Для передачи {audit.TransferId} не найдены связанные операции.");
            }

            var fromAuditOperation = audit.Operations.FirstOrDefault(x => x.QuantityChange < 0);
            var toAuditOperation = audit.Operations.FirstOrDefault(x => x.QuantityChange > 0);

            if (fromAuditOperation is null || toAuditOperation is null)
            {
                throw new InvalidOperationException($"Аудит {transferAuditId} содержит некорректные операции.");
            }

            var fromBalance = await _dbContext.WipBalances
                .FirstOrDefaultAsync(
                    x => x.PartId == audit.PartId &&
                         x.SectionId == audit.FromSectionId &&
                         x.OpNumber == audit.FromOpNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            if (fromBalance is null)
            {
                throw new InvalidOperationException(
                    $"Остаток НЗП для операции {audit.FromOpNumber} детали {audit.PartId} не найден.");
            }

            var fromBalanceBefore = fromBalance.Quantity;
            fromBalance.Quantity = fromAuditOperation.BalanceBefore;

            Guid? transferLabelId = audit.WipLabelId;
            string? transferLabelNumber = audit.LabelNumber;
            var labelQuantityBefore = audit.LabelQuantityAfter;
            var labelQuantityAfter = audit.LabelQuantityBefore;

            TransferDeleteWarehouseItemDto? warehouseDto = null;
            decimal toBalanceBefore;
            decimal toBalanceAfter;
            if (audit.IsWarehouseTransfer)
            {
                var warehouseItem = transfer.WarehouseItem ?? await _dbContext.WarehouseItems
                    .FirstOrDefaultAsync(x => x.TransferId == transfer.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (warehouseItem is null)
                {
                    throw new InvalidOperationException($"Для передачи {transfer.Id} не найдено складское движение.");
                }

                toBalanceBefore = await _dbContext.WarehouseItems
                    .Where(x => x.PartId == audit.PartId)
                    .SumAsync(x => x.Quantity, cancellationToken)
                    .ConfigureAwait(false);

                toBalanceAfter = toBalanceBefore - warehouseItem.Quantity;
                if (toBalanceAfter < -0.000001m)
                {
                    throw new InvalidOperationException("Откат передачи приведёт к отрицательному остатку на складе.");
                }

                warehouseDto = new TransferDeleteWarehouseItemDto(warehouseItem.Id, warehouseItem.Quantity);
                _dbContext.WarehouseItems.Remove(warehouseItem);
            }
            else
            {
                var toBalance = await _dbContext.WipBalances
                    .FirstOrDefaultAsync(
                        x => x.PartId == audit.PartId &&
                             x.SectionId == audit.ToSectionId &&
                             x.OpNumber == audit.ToOpNumber,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (toBalance is null)
                {
                    throw new InvalidOperationException(
                        $"Остаток НЗП для операции {audit.ToOpNumber} детали {audit.PartId} не найден.");
                }

                toBalanceBefore = toBalance.Quantity;
                toBalanceAfter = toAuditOperation.BalanceBefore;
                if (toBalanceAfter < -0.000001m)
                {
                    throw new InvalidOperationException("Откат передачи приведёт к отрицательному остатку на операции после.");
                }

                toBalance.Quantity = toBalanceAfter;
            }

            TransferDeleteScrapDto? scrapDto = null;
            if (transfer.Scrap is not null)
            {
                var scrap = transfer.Scrap;
                scrapDto = new TransferDeleteScrapDto(scrap.Id, scrap.ScrapType, scrap.Quantity, scrap.Comment);
                _dbContext.WipScraps.Remove(scrap);
            }

            if (transferLabelId.HasValue)
            {
                var label = transfer.WipLabel ?? await _dbContext.WipLabels
                    .FirstOrDefaultAsync(x => x.Id == transferLabelId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (label is null)
                {
                    throw new InvalidOperationException($"Для передачи {transfer.Id} не найден ярлык {transferLabelId.Value}.");
                }

                var restoredRemaining = audit.LabelQuantityBefore ?? label.RemainingQuantity;
                label.RemainingQuantity = restoredRemaining;
                label.Status = WipLabelInvariants.GetStatusAfterConsume(restoredRemaining);
                label.CurrentSectionId = audit.ToSectionId;
                label.CurrentOpNumber = audit.ToOpNumber;
                transferLabelNumber = label.Number;

                await _dbContext.WipLabelLedger.AddAsync(new WipLabelLedger
                {
                    EventId = Guid.NewGuid(),
                    EventTime = now,
                    UserId = userId,
                    TransactionId = revertTransactionId,
                    EventType = WipLabelEventType.Revert,
                    FromLabelId = label.Id,
                    ToLabelId = label.Id,
                    FromSectionId = audit.FromSectionId,
                    FromOpNumber = audit.FromOpNumber,
                    ToSectionId = audit.ToSectionId,
                    ToOpNumber = audit.ToOpNumber,
                    Qty = audit.Quantity,
                    ScrapQty = audit.ScrapQuantity,
                    RefEntityType = nameof(TransferAudit),
                    RefEntityId = audit.Id,
                }, cancellationToken).ConfigureAwait(false);
            }

            if (operations.Count > 0)
            {
                _dbContext.WipTransferOperations.RemoveRange(operations);
            }

            _dbContext.WipTransfers.Remove(transfer);

            audit.IsReverted = true;
            audit.RevertedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new TransferDeleteResultDto(
                transfer.Id,
                audit.PartId,
                audit.FromOpNumber,
                audit.FromSectionId,
                fromBalanceBefore,
                fromBalance.Quantity,
                audit.ToOpNumber,
                audit.ToSectionId,
                toBalanceBefore,
                toBalanceAfter,
                audit.Quantity,
                audit.IsWarehouseTransfer,
                operations.Select(x => x.Id).ToArray(),
                scrapDto,
                warehouseDto,
                transferLabelId,
                transferLabelNumber,
                labelQuantityBefore,
                labelQuantityAfter);
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

    private sealed record ResolvedTransferLabelUsage(WipLabel Label, decimal RemainingBefore, decimal RemainingAfter);
}
