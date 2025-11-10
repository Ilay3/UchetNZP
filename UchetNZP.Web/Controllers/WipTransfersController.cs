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
                              select new TransferOperationLookupViewModel(
                                  OperationNumber.Format(route.OpNumber),
                                  operation != null ? operation.Name : string.Empty,
                                  route.NormHours,
                                  balance != null ? balance.Quantity : 0m,
                                  false);

        var operations = await operationsQuery
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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
            true));

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

        var labelLookup = await LoadLabelNumbersAsync(labelKeys, cancellationToken).ConfigureAwait(false);

        var fromLabels = labelLookup.TryGetValue((partId, fromRoute.SectionId, fromOp), out var fromList)
            ? fromList
            : Array.Empty<string>();

        var toLabels = isWarehouseTransfer
            ? Array.Empty<string>()
            : (labelLookup.TryGetValue((partId, toSectionId, toOp), out var toList) ? toList : Array.Empty<string>());

        var fromModel = new TransferOperationBalanceViewModel(
            OperationNumber.Format(fromOp),
            fromRoute.SectionId,
            fromBalance,
            fromLabels);

        var toModel = new TransferOperationBalanceViewModel(
            OperationNumber.Format(toOp),
            toSectionId,
            toBalanceValue,
            toLabels);

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

        var labels = await _dbContext.WipLabels
            .AsNoTracking()
            .Where(x => x.PartId == partId && x.RemainingQuantity > 0m)
            .Where(x => x.WipReceipt != null && x.WipReceipt.SectionId == route.SectionId && x.WipReceipt.OpNumber == parsedOpNumber)
            .OrderBy(x => x.Number)
            .Select(x => new TransferLabelOptionViewModel(
                x.Id,
                x.Number,
                x.Quantity,
                x.RemainingQuantity))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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

        var summary = await _transferService.AddTransfersBatchAsync(dtos, cancellationToken).ConfigureAwait(false);

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
                        scrapSummary,
                        labels,
                        item.WipLabelId,
                        item.LabelNumber,
                        item.LabelQuantityBefore,
                        item.LabelQuantityAfter);
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

    private async Task<Dictionary<(Guid PartId, Guid SectionId, int OpNumber), IReadOnlyList<string>>> LoadLabelNumbersAsync(
        IReadOnlyCollection<(Guid PartId, Guid SectionId, int OpNumber)> in_keys,
        CancellationToken in_cancellationToken)
    {
        if (in_keys is null)
        {
            throw new ArgumentNullException(nameof(in_keys));
        }

        var buffer = new Dictionary<(Guid, Guid, int), List<string>>();
        if (in_keys.Count == 0)
        {
            return new Dictionary<(Guid, Guid, int), IReadOnlyList<string>>();
        }

        var partIds = in_keys.Select(x => x.PartId).Distinct().ToList();
        var sectionIds = in_keys.Select(x => x.SectionId).Distinct().ToList();
        var opNumbers = in_keys.Select(x => x.OpNumber).Distinct().ToList();

        var labels = await _dbContext.WipLabels
            .AsNoTracking()
            .Where(x => partIds.Contains(x.PartId) && x.RemainingQuantity > 0m)
            .Where(x => x.WipReceipt != null && sectionIds.Contains(x.WipReceipt.SectionId) && opNumbers.Contains(x.WipReceipt.OpNumber))
            .Select(x => new
            {
                x.PartId,
                SectionId = x.WipReceipt!.SectionId,
                OpNumber = x.WipReceipt.OpNumber,
                x.Number,
            })
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        foreach (var label in labels)
        {
            var key = (label.PartId, label.SectionId, label.OpNumber);
            if (!buffer.TryGetValue(key, out var list))
            {
                list = new List<string>();
                buffer[key] = list;
            }

            if (!string.IsNullOrWhiteSpace(label.Number))
            {
                list.Add(label.Number.Trim());
            }
        }

        foreach (var pair in buffer.ToList())
        {
            var sorted = pair.Value
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            buffer[pair.Key] = sorted;
        }

        var ret = buffer.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value);

        foreach (var key in in_keys)
        {
            if (!ret.ContainsKey(key))
            {
                ret[key] = Array.Empty<string>();
            }
        }

        return ret;
    }

}
