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
using UchetNZP.Web.Services;
using UchetNZP.Shared;

namespace UchetNZP.Web.Controllers;

[Route("wip/transfer")]
public class WipTransfersController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ITransferService _transferService;
    private readonly IWipLabelLookupService _labelLookupService;

    public WipTransfersController(
        AppDbContext dbContext,
        ITransferService transferService,
        IWipLabelLookupService labelLookupService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
        _labelLookupService = labelLookupService ?? throw new ArgumentNullException(nameof(labelLookupService));
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

        var labelKeys = new List<LabelLookupKey>
        {
            new LabelLookupKey(partId, fromRoute.SectionId, fromOp),
        };

        if (!isWarehouseTransfer)
        {
            labelKeys.Add(new LabelLookupKey(partId, toSectionId, toOp));
        }

        var labelLookup = await _labelLookupService
            .LoadAsync(labelKeys, cancellationToken, null, DateTime.UtcNow.AddDays(1))
            .ConfigureAwait(false);

        var nowUtc = DateTime.UtcNow;
        var fromLabels = _labelLookupService.FindLabelsUpToDate(
            labelLookup,
            new LabelLookupKey(partId, fromRoute.SectionId, fromOp),
            nowUtc);

        var toLabels = isWarehouseTransfer
            ? Array.Empty<string>()
            : _labelLookupService.FindLabelsUpToDate(
                labelLookup,
                new LabelLookupKey(partId, toSectionId, toOp),
                nowUtc);

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
            var transferIds = summary.Items
                .Select(x => x.TransferId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var transferDateMap = transferIds.Count == 0
                ? new Dictionary<Guid, DateTime>()
                : await _dbContext.WipTransfers
                    .AsNoTracking()
                    .Where(x => transferIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => x.TransferDate, cancellationToken)
                    .ConfigureAwait(false);

            var labelKeys = summary.Items
                .Select(x => new LabelLookupKey(x.PartId, x.FromSectionId, x.FromOpNumber))
                .ToList();

            DateTime? maxTransferDateUtc = transferDateMap.Count == 0
                ? null
                : transferDateMap.Values
                    .Select(EnsureUtc)
                    .DefaultIfEmpty(DateTime.UtcNow)
                    .Max()
                    .AddDays(1);

            var labelLookup = await _labelLookupService
                .LoadAsync(labelKeys, cancellationToken, null, maxTransferDateUtc)
                .ConfigureAwait(false);

            summaryItems = summary.Items
                .Select(item =>
                {
                    var hasDate = transferDateMap.TryGetValue(item.TransferId, out var transferDate);
                    var effectiveDate = hasDate ? transferDate : DateTime.UtcNow;

                    var labels = _labelLookupService.FindLabelsUpToDate(
                        labelLookup,
                        new LabelLookupKey(item.PartId, item.FromSectionId, item.FromOpNumber),
                        effectiveDate);

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
                        labels);
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
                        result.WarehouseItem.Quantity));

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
        TransferScrapSaveItem? Scrap);

    public record TransferScrapSaveItem(
        ScrapType ScrapType,
        decimal Quantity,
        string? Comment);

    private static DateTime EnsureUtc(DateTime in_value)
    {
        return in_value.Kind switch
        {
            DateTimeKind.Utc => in_value,
            DateTimeKind.Local => in_value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(in_value, DateTimeKind.Utc),
        };
    }
}
