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

    public WipTransfersController(AppDbContext dbContext, ITransferService transferService)
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

        TransferOperationBalanceViewModel toModel;
        if (toOp == WarehouseDefaults.OperationNumber)
        {
            var warehouseBalance = await _dbContext.WarehouseItems
                .AsNoTracking()
                .Where(x => x.PartId == partId)
                .SumAsync(x => x.Quantity, cancellationToken)
                .ConfigureAwait(false);

            toModel = new TransferOperationBalanceViewModel(
                OperationNumber.Format(toOp),
                WarehouseDefaults.SectionId,
                warehouseBalance);
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

            toModel = new TransferOperationBalanceViewModel(
                OperationNumber.Format(toOp),
                toRoute.SectionId,
                toBalance);
        }

        var model = new TransferBalancesViewModel(
            new TransferOperationBalanceViewModel(OperationNumber.Format(fromOp), fromRoute.SectionId, fromBalance),
            toModel);

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

        var model = new TransferBatchSummaryViewModel(
            summary.Saved,
            summary.Items
                .Select(x => new TransferSummaryItemViewModel(
                    x.PartId,
                    OperationNumber.Format(x.FromOpNumber),
                    x.FromSectionId,
                    x.FromBalanceBefore,
                    x.FromBalanceAfter,
                    OperationNumber.Format(x.ToOpNumber),
                    x.ToSectionId,
                    x.ToBalanceBefore,
                    x.ToBalanceAfter,
                    x.Quantity,
                    x.TransferId,
                    x.Scrap is null
                        ? null
                        : new TransferScrapSummaryViewModel(x.Scrap.ScrapId, x.Scrap.ScrapType, x.Scrap.Quantity, x.Scrap.Comment)))
                .ToList());

        return Ok(model);
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
}
