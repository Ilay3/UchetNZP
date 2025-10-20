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
                                  route.OpNumber,
                                  operation != null ? operation.Name : string.Empty,
                                  route.NormHours,
                                  balance != null ? balance.Quantity : 0m);

        var operations = await operationsQuery
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(operations);
    }

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] Guid partId,
        [FromQuery] int fromOpNumber,
        [FromQuery] int toOpNumber,
        CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty || fromOpNumber <= 0 || toOpNumber <= 0)
        {
            return BadRequest("Недостаточно данных для определения остатков.");
        }

        var routeSections = await _dbContext.PartRoutes
            .AsNoTracking()
            .Where(x => x.PartId == partId && (x.OpNumber == fromOpNumber || x.OpNumber == toOpNumber))
            .Select(x => new { x.OpNumber, x.SectionId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fromRoute = routeSections.FirstOrDefault(x => x.OpNumber == fromOpNumber);
        var toRoute = routeSections.FirstOrDefault(x => x.OpNumber == toOpNumber);

        if (fromRoute is null || toRoute is null)
        {
            return BadRequest("Маршрут для одной из операций не найден.");
        }

        var balances = await _dbContext.WipBalances
            .AsNoTracking()
            .Where(x =>
                x.PartId == partId &&
                ((x.OpNumber == fromOpNumber && x.SectionId == fromRoute.SectionId) ||
                 (x.OpNumber == toOpNumber && x.SectionId == toRoute.SectionId)))
            .Select(x => new { x.OpNumber, x.SectionId, x.Quantity })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal fromBalance = balances
            .Where(x => x.OpNumber == fromOpNumber && x.SectionId == fromRoute.SectionId)
            .Select(x => x.Quantity)
            .FirstOrDefault();

        decimal toBalance = balances
            .Where(x => x.OpNumber == toOpNumber && x.SectionId == toRoute.SectionId)
            .Select(x => x.Quantity)
            .FirstOrDefault();

        var model = new TransferBalancesViewModel(
            new TransferOperationBalanceViewModel(fromOpNumber, fromRoute.SectionId, fromBalance),
            new TransferOperationBalanceViewModel(toOpNumber, toRoute.SectionId, toBalance));

        return Ok(model);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] TransferSaveRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("Список передач пуст.");
        }

        var dtos = request.Items
            .Select(x => new TransferItemDto(
                x.PartId,
                x.FromOpNumber,
                x.ToOpNumber,
                x.TransferDate,
                x.Quantity,
                x.Comment,
                x.Scrap is null
                    ? null
                    : new TransferScrapDto(x.Scrap.ScrapType, x.Scrap.Quantity, x.Scrap.Comment)))
            .ToList();

        var summary = await _transferService.AddTransfersBatchAsync(dtos, cancellationToken).ConfigureAwait(false);

        var model = new TransferBatchSummaryViewModel(
            summary.Saved,
            summary.Items
                .Select(x => new TransferSummaryItemViewModel(
                    x.PartId,
                    x.FromOpNumber,
                    x.FromSectionId,
                    x.FromBalanceBefore,
                    x.FromBalanceAfter,
                    x.ToOpNumber,
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
        int FromOpNumber,
        int ToOpNumber,
        DateTime TransferDate,
        decimal Quantity,
        string? Comment,
        TransferScrapSaveItem? Scrap);

    public record TransferScrapSaveItem(
        ScrapType ScrapType,
        decimal Quantity,
        string? Comment);
}
