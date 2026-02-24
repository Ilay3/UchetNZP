using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Transfers;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Shared;

namespace UchetNZP.Web.Controllers;

[Route("wip/transfer")]
public class WipTransfersController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ITransferService _transferService;

    public WipTransfersController(
        AppDbContext dbContext,
        ITransferService transferService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View("~/Views/Wip/Transfer.cshtml");
    }

    [HttpGet("parts")]
    public async Task<IActionResult> GetParts([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Parts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                (x.Code != null && x.Code.ToLower().Contains(term)));
        }

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpGet("operations")]
    public async Task<IActionResult> GetOperations([FromQuery] Guid partId, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty)
        {
            return BadRequest("Не выбрана деталь.");
        }

        var operationsQuery = from route in _dbContext.PartRoutes.AsNoTracking()
                              where route.PartId == partId
                              join operationEntity in _dbContext.Operations.AsNoTracking() on route.OperationId equals operationEntity.Id into operationGroup
                              from operation in operationGroup.DefaultIfEmpty()
                              join balance in _dbContext.WipBalances.AsNoTracking()
                                  on new { route.PartId, route.SectionId, route.OpNumber }
                                  equals new { balance.PartId, balance.SectionId, balance.OpNumber } into balances
                              from balance in balances.DefaultIfEmpty()
                              orderby route.OpNumber
                              select new
                              {
                                  route.OpNumber,
                                  route.SectionId,
                                  OperationName = operation != null ? operation.Name : string.Empty,
                                  route.NormHours,
                                  Balance = balance != null ? balance.Quantity : 0m,
                              };

        var operationItems = await operationsQuery
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var labelKeys = operationItems
            .Select(x => (partId, x.SectionId, x.OpNumber))
            .Distinct()
            .ToList();

        var labelLookup = await LoadLabelBalancesAsync(labelKeys, cancellationToken).ConfigureAwait(false);

        var operations = operationItems
            .Select(item =>
            {
                var key = (partId, item.SectionId, item.OpNumber);
                var labels = labelLookup.TryGetValue(key, out var list)
                    ? list
                    : Array.Empty<TransferOperationLabelBalanceViewModel>();

                return new TransferOperationLookupViewModel(
                    OperationNumber.Format(item.OpNumber),
                    item.OperationName,
                    item.NormHours,
                    item.Balance,
                    false,
                    labels);
            })
            .ToList();

        var warehouseBalance = await _dbContext.WarehouseItems
            .AsNoTracking()
            .Where(x => x.PartId == partId)
            .SumAsync(x => x.Quantity, cancellationToken)
            .ConfigureAwait(false);

        operations.Add(new TransferOperationLookupViewModel(
            OperationNumber.Format(WarehouseDefaults.OperationNumber),
            WarehouseDefaults.OperationName,
            0m,
            warehouseBalance,
            true,
            Array.Empty<TransferOperationLabelBalanceViewModel>()));

        return Ok(operations);
    }

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] Guid partId,
        [FromQuery] string? fromOpNumber,
        [FromQuery] string? toOpNumber,
        CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty || string.IsNullOrWhiteSpace(fromOpNumber) || string.IsNullOrWhiteSpace(toOpNumber))
        {
            return BadRequest("Недостаточно данных для определения остатков.");
        }

        int fromOp;
        int toOp;
        try
        {
            fromOp = OperationNumber.Parse(fromOpNumber, nameof(fromOpNumber));
            toOp = OperationNumber.Parse(toOpNumber, nameof(toOpNumber));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var fromRoute = await _dbContext.PartRoutes
            .AsNoTracking()
            .Where(x => x.PartId == partId && x.OpNumber == fromOp)
            .Select(x => new { x.OpNumber, x.SectionId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (fromRoute is null)
        {
            return BadRequest("Маршрут для операции до не найден.");
        }

        var fromBalance = await _dbContext.WipBalances
            .AsNoTracking()
            .Where(x => x.PartId == partId && x.OpNumber == fromOp && x.SectionId == fromRoute.SectionId)
            .Select(x => x.Quantity)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var isWarehouseTransfer = toOp == WarehouseDefaults.OperationNumber;
        Guid toSectionId;
        decimal toBalanceValue;

        if (isWarehouseTransfer)
        {
            var warehouseBalance = await _dbContext.WarehouseItems
                .AsNoTracking()
                .Where(x => x.PartId == partId)
                .SumAsync(x => x.Quantity, cancellationToken)
                .ConfigureAwait(false);

            toSectionId = WarehouseDefaults.SectionId;
            toBalanceValue = warehouseBalance;
        }
        else
        {
            var toRoute = await _dbContext.PartRoutes
                .AsNoTracking()
                .Where(x => x.PartId == partId && x.OpNumber == toOp)
                .Select(x => new { x.OpNumber, x.SectionId })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (toRoute is null)
            {
                return BadRequest("Маршрут для операции после не найден.");
            }

            var toBalance = await _dbContext.WipBalances
                .AsNoTracking()
                .Where(x => x.PartId == partId && x.OpNumber == toOp && x.SectionId == toRoute.SectionId)
                .Select(x => x.Quantity)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            toSectionId = toRoute.SectionId;
            toBalanceValue = toBalance;
        }

        var labelKeys = new List<(Guid PartId, Guid SectionId, int OpNumber)>
        {
            (partId, fromRoute.SectionId, fromOp),
        };

        if (!isWarehouseTransfer)
        {
            labelKeys.Add((partId, toSectionId, toOp));
        }

        var labelLookup = await LoadLabelBalancesAsync(labelKeys, cancellationToken).ConfigureAwait(false);

        var fromLabelBalances = labelLookup.TryGetValue((partId, fromRoute.SectionId, fromOp), out var fromList)
            ? fromList
            : Array.Empty<TransferOperationLabelBalanceViewModel>();

        var toLabelBalances = isWarehouseTransfer
            ? Array.Empty<TransferOperationLabelBalanceViewModel>()
            : (labelLookup.TryGetValue((partId, toSectionId, toOp), out var toList) ? toList : Array.Empty<TransferOperationLabelBalanceViewModel>());

        var fromLabels = ExtractLabelNumbers(fromLabelBalances);
        var toLabels = ExtractLabelNumbers(toLabelBalances);

        var fromModel = new TransferOperationBalanceViewModel(
            OperationNumber.Format(fromOp),
            fromRoute.SectionId,
            fromBalance,
            fromLabels,
            fromLabelBalances);

        var toModel = new TransferOperationBalanceViewModel(
            OperationNumber.Format(toOp),
            toSectionId,
            toBalanceValue,
            toLabels,
            toLabelBalances);

        var model = new TransferBalancesViewModel(fromModel, toModel);

        return Ok(model);
    }

    [HttpGet("labels")]
    public async Task<IActionResult> GetLabels([FromQuery] Guid partId, [FromQuery] string? opNumber, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty)
        {
            return BadRequest("Не выбрана деталь.");
        }

        if (string.IsNullOrWhiteSpace(opNumber))
        {
            return BadRequest("Не указан номер операции.");
        }

        int parsedOpNumber;
        try
        {
            parsedOpNumber = OperationNumber.Parse(opNumber, nameof(opNumber));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var route = await _dbContext.PartRoutes
            .AsNoTracking()
            .Where(x => x.PartId == partId && x.OpNumber == parsedOpNumber)
            .Select(x => new { x.SectionId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (route is null)
        {
            return BadRequest("Маршрут для указанной операции не найден.");
        }

        var availableLabels = await _dbContext.WipLabels
            .AsNoTracking()
            .Where(x => x.PartId == partId)
            .Select(x => new { x.Id, x.Number, x.Quantity })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var labelBalances = await LoadLabelBalancesAsync(
                new[] { (partId, route.SectionId, parsedOpNumber) },
                cancellationToken)
            .ConfigureAwait(false);

        var balancesForOperation = labelBalances.TryGetValue((partId, route.SectionId, parsedOpNumber), out var current)
            ? current
            : Array.Empty<TransferOperationLabelBalanceViewModel>();

        var labels = balancesForOperation
            .Join(
                availableLabels,
                balance => balance.Id,
                label => label.Id,
                (balance, label) => new TransferLabelOptionViewModel(
                    label.Id,
                    label.Number,
                    label.Quantity,
                    balance.RemainingQuantity))
            .OrderBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(labels);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] TransferSaveRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("Список передач пуст.");
        }

        List<TransferItemDto> dtos;
        try
        {
            dtos = request.Items
                .Select(x => new TransferItemDto(
                    x.PartId,
                    OperationNumber.Parse(x.FromOpNumber, nameof(TransferSaveItem.FromOpNumber)),
                    OperationNumber.Parse(x.ToOpNumber, nameof(TransferSaveItem.ToOpNumber)),
                    x.TransferDate,
                    x.Quantity,
                    x.Comment,
                    x.WipLabelId,
                    x.Scrap is null
                        ? null
                        : new TransferScrapDto(x.Scrap.ScrapType, x.Scrap.Quantity, x.Scrap.Comment)))
                .ToList();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        TransferBatchSummaryDto summary;
        try
        {
            summary = await _transferService.AddTransfersBatchAsync(dtos, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        List<TransferSummaryItemViewModel> summaryItems;
        if (summary.Items.Count == 0)
        {
            summaryItems = new List<TransferSummaryItemViewModel>();
        }
        else
        {
            var labelKeys = summary.Items
                .Select(x => (x.PartId, x.FromSectionId, x.FromOpNumber))
                .Distinct()
                .ToList();

                    var labelLookup = await LoadLabelNumbersAsync(labelKeys, cancellationToken).ConfigureAwait(false);

                    summaryItems = summary.Items
                        .Select(item =>
                        {
                    var key = (item.PartId, item.FromSectionId, item.FromOpNumber);
                    var labels = labelLookup.TryGetValue(key, out var list) ? list : Array.Empty<string>();

                    var scrapSummary = item.Scrap is null
                        ? null
                        : new TransferScrapSummaryViewModel(
                            item.Scrap.ScrapId,
                            item.Scrap.ScrapType,
                            item.Scrap.Quantity,
                            item.Scrap.Comment);

                            return new TransferSummaryItemViewModel(
                                item.PartId,
                                OperationNumber.Format(item.FromOpNumber),
                                item.FromSectionId,
                                item.FromBalanceBefore,
                                item.FromBalanceAfter,
                                OperationNumber.Format(item.ToOpNumber),
                                item.ToSectionId,
                                item.ToBalanceBefore,
                                item.ToBalanceAfter,
                                item.Quantity,
                                item.TransferId,
                                item.TransferAuditId,
                                item.TransactionId,
                                scrapSummary,
                                labels,
                                item.WipLabelId,
                                item.LabelNumber,
                                item.LabelQuantityBefore,
                                item.LabelQuantityAfter,
                                item.IsReverted);
                        })
                        .ToList();
                }

        var model = new TransferBatchSummaryViewModel(summary.Saved, summaryItems);

        return Ok(model);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Не указан идентификатор передачи.");
        }

        try
        {
            var result = await _transferService.DeleteTransferAsync(id, cancellationToken).ConfigureAwait(false);

            var viewModel = new TransferDeleteResultViewModel(
                result.TransferId,
                result.PartId,
                OperationNumber.Format(result.FromOpNumber),
                result.FromSectionId,
                result.FromBalanceBefore,
                result.FromBalanceAfter,
                OperationNumber.Format(result.ToOpNumber),
                result.ToSectionId,
                result.ToBalanceBefore,
                result.ToBalanceAfter,
                result.Quantity,
                result.IsWarehouseTransfer,
                result.DeletedOperationIds,
                result.Scrap is null
                    ? null
                    : new TransferDeleteScrapViewModel(
                        result.Scrap.ScrapId,
                        result.Scrap.ScrapType,
                        result.Scrap.Quantity,
                        result.Scrap.Comment),
                result.WarehouseItem is null
                    ? null
                    : new TransferDeleteWarehouseItemViewModel(
                        result.WarehouseItem.WarehouseItemId,
                        result.WarehouseItem.Quantity),
                result.WipLabelId,
                result.LabelNumber,
                result.LabelQuantityBefore,
                result.LabelQuantityAfter);

            return Ok(viewModel);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("revert/{auditId:guid}")]
    public async Task<IActionResult> Revert(Guid auditId, CancellationToken cancellationToken)
    {
        if (auditId == Guid.Empty)
        {
            return BadRequest("Не указан идентификатор аудита передачи.");
        }

        try
        {
            var result = await _transferService.RevertTransferAsync(auditId, cancellationToken).ConfigureAwait(false);

            var viewModel = new TransferDeleteResultViewModel(
                result.TransferId,
                result.PartId,
                OperationNumber.Format(result.FromOpNumber),
                result.FromSectionId,
                result.FromBalanceBefore,
                result.FromBalanceAfter,
                OperationNumber.Format(result.ToOpNumber),
                result.ToSectionId,
                result.ToBalanceBefore,
                result.ToBalanceAfter,
                result.Quantity,
                result.IsWarehouseTransfer,
                result.DeletedOperationIds,
                result.Scrap is null
                    ? null
                    : new TransferDeleteScrapViewModel(
                        result.Scrap.ScrapId,
                        result.Scrap.ScrapType,
                        result.Scrap.Quantity,
                        result.Scrap.Comment),
                result.WarehouseItem is null
                    ? null
                    : new TransferDeleteWarehouseItemViewModel(
                        result.WarehouseItem.WarehouseItemId,
                        result.WarehouseItem.Quantity),
                result.WipLabelId,
                result.LabelNumber,
                result.LabelQuantityBefore,
                result.LabelQuantityAfter);

            return Ok(viewModel);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public record TransferSaveRequest(IReadOnlyList<TransferSaveItem> Items);

    public record TransferSaveItem(
        Guid PartId,
        string FromOpNumber,
        string ToOpNumber,
        DateTime TransferDate,
        decimal Quantity,
        string? Comment,
        Guid? WipLabelId,
        TransferScrapSaveItem? Scrap);

    public record TransferScrapSaveItem(
        ScrapType ScrapType,
        decimal Quantity,
        string? Comment);

    private async Task<Dictionary<(Guid PartId, Guid SectionId, int OpNumber), IReadOnlyList<TransferOperationLabelBalanceViewModel>>> LoadLabelBalancesAsync(
        IReadOnlyCollection<(Guid PartId, Guid SectionId, int OpNumber)> in_keys,
        CancellationToken in_cancellationToken)
    {
        if (in_keys is null)
        {
            throw new ArgumentNullException(nameof(in_keys));
        }

        var ret = new Dictionary<(Guid, Guid, int), IReadOnlyList<TransferOperationLabelBalanceViewModel>>();
        if (in_keys.Count == 0)
        {
            return ret;
        }

        var partIds = in_keys.Select(x => x.PartId).Distinct().ToList();
        var sectionIds = in_keys.Select(x => x.SectionId).Distinct().ToList();
        var opNumbers = in_keys.Select(x => x.OpNumber).Distinct().ToList();

        var receiptLabels = await _dbContext.WipReceipts
            .AsNoTracking()
            .Where(x =>
                x.WipLabelId != null &&
                partIds.Contains(x.PartId) &&
                sectionIds.Contains(x.SectionId) &&
                opNumbers.Contains(x.OpNumber))
            .GroupBy(x => new { x.PartId, x.SectionId, x.OpNumber, LabelId = x.WipLabelId!.Value })
            .Select(g => new
            {
                g.Key.PartId,
                g.Key.SectionId,
                g.Key.OpNumber,
                LabelId = g.Key.LabelId,
                Quantity = g.Sum(x => x.Quantity),
            })
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var transferLabels = await _dbContext.TransferAudits
            .AsNoTracking()
            .Where(x => !x.IsReverted && x.WipLabelId != null && partIds.Contains(x.PartId))
            .Select(x => new
            {
                x.PartId,
                x.FromSectionId,
                x.FromOpNumber,
                x.ToSectionId,
                x.ToOpNumber,
                x.Quantity,
                x.ScrapQuantity,
                x.IsWarehouseTransfer,
                LabelId = x.WipLabelId!.Value,
            })
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var labelIds = receiptLabels
            .Select(x => x.LabelId)
            .Concat(transferLabels.Select(x => x.LabelId))
            .Distinct()
            .ToList();

        var labelLookup = labelIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.WipLabels
                .AsNoTracking()
                .Where(x => labelIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Number ?? string.Empty, in_cancellationToken)
                .ConfigureAwait(false);

        var byKey = new Dictionary<(Guid PartId, Guid SectionId, int OpNumber, Guid LabelId), decimal>();

        foreach (var item in receiptLabels)
        {
            var key = (item.PartId, item.SectionId, item.OpNumber, item.LabelId);
            byKey[key] = byKey.TryGetValue(key, out var existing) ? existing + item.Quantity : item.Quantity;
        }

        foreach (var transfer in transferLabels)
        {
            var fromKey = (transfer.PartId, transfer.FromSectionId, transfer.FromOpNumber, transfer.LabelId);
            var fromDelta = transfer.Quantity + transfer.ScrapQuantity;
            byKey[fromKey] = byKey.TryGetValue(fromKey, out var existingFrom) ? existingFrom - fromDelta : -fromDelta;

            if (!transfer.IsWarehouseTransfer)
            {
                var toKey = (transfer.PartId, transfer.ToSectionId, transfer.ToOpNumber, transfer.LabelId);
                byKey[toKey] = byKey.TryGetValue(toKey, out var existingTo) ? existingTo + transfer.Quantity : transfer.Quantity;
            }
        }

        var buffer = new Dictionary<(Guid, Guid, int), List<TransferOperationLabelBalanceViewModel>>();

        foreach (var pair in byKey.Where(x => x.Value > 0m))
        {
            var key = (pair.Key.PartId, pair.Key.SectionId, pair.Key.OpNumber);
            if (!buffer.TryGetValue(key, out var list))
            {
                list = new List<TransferOperationLabelBalanceViewModel>();
                buffer[key] = list;
            }

            var number = labelLookup.TryGetValue(pair.Key.LabelId, out var labelNumber) && !string.IsNullOrWhiteSpace(labelNumber)
                ? labelNumber.Trim()
                : string.Empty;

            list.Add(new TransferOperationLabelBalanceViewModel(pair.Key.LabelId, number, pair.Value));
        }

        foreach (var pair in buffer.ToList())
        {
            var sorted = pair.Value
                .OrderBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id)
                .ToList();

            buffer[pair.Key] = sorted;
        }

        foreach (var pair in buffer)
        {
            ret[pair.Key] = pair.Value;
        }

        foreach (var key in in_keys)
        {
            if (!ret.ContainsKey(key))
            {
                ret[key] = Array.Empty<TransferOperationLabelBalanceViewModel>();
            }
        }

        return ret;
    }

    private async Task<Dictionary<(Guid PartId, Guid SectionId, int OpNumber), IReadOnlyList<string>>> LoadLabelNumbersAsync(
        IReadOnlyCollection<(Guid PartId, Guid SectionId, int OpNumber)> in_keys,
        CancellationToken in_cancellationToken)
    {
        var balances = await LoadLabelBalancesAsync(in_keys, in_cancellationToken).ConfigureAwait(false);

        var ret = new Dictionary<(Guid, Guid, int), IReadOnlyList<string>>();
        foreach (var key in in_keys)
        {
            if (!balances.TryGetValue(key, out var list) || list.Count == 0)
            {
                ret[key] = Array.Empty<string>();
                continue;
            }

            ret[key] = ExtractLabelNumbers(list);
        }

        return ret;
    }

    private static IReadOnlyList<string> ExtractLabelNumbers(IReadOnlyList<TransferOperationLabelBalanceViewModel> in_labels)
    {
        if (in_labels is null || in_labels.Count == 0)
        {
            return Array.Empty<string>();
        }

        var ret = in_labels
            .Select(x => x.Number)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ret;
    }

}
