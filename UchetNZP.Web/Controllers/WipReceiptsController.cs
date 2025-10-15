using System;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("wip/receipts")]
public class WipReceiptsController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IWipService _wipService;

    public WipReceiptsController(AppDbContext dbContext, IWipService wipService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _wipService = wipService ?? throw new ArgumentNullException(nameof(wipService));
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View("~/Views/Wip/Receipts.cshtml");
    }

    [HttpGet("sections")]
    public async Task<IActionResult> GetSections([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Sections.AsNoTracking();

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

        var operations = await _dbContext.PartRoutes
            .AsNoTracking()
            .Where(x => x.PartId == partId)
            .Include(x => x.Operation)
            .Include(x => x.Section)
            .OrderBy(x => x.OpNumber)
            .Select(x => new PartOperationViewModel(
                x.PartId,
                x.OpNumber,
                x.OperationId,
                x.Operation != null ? x.Operation.Name : string.Empty,
                x.SectionId,
                x.Section != null ? x.Section.Name : string.Empty,
                x.NormHours))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(operations);
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] Guid partId, [FromQuery] Guid sectionId, [FromQuery] int opNumber, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty || sectionId == Guid.Empty || opNumber <= 0)
        {
            return BadRequest("Недостаточно данных для определения остатка.");
        }

        var balance = await _dbContext.WipBalances
            .AsNoTracking()
            .Where(x => x.PartId == partId && x.SectionId == sectionId && x.OpNumber == opNumber)
            .Select(x => x.Quantity)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(balance);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] ReceiptSaveRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("Список приходов пуст.");
        }

        var dtos = request.Items.Select(x => new ReceiptItemDto(
            x.PartId,
            x.OpNumber,
            x.SectionId,
            x.ReceiptDate,
            x.Quantity,
            x.DocumentNumber,
            x.Comment)).ToList();

        var summary = await _wipService.AddReceiptsBatchAsync(dtos, cancellationToken).ConfigureAwait(false);

        var model = new ReceiptBatchSummaryViewModel(
            summary.Saved,
            summary.Items
                .Select(x => new ReceiptSummaryItemViewModel(x.PartId, x.OpNumber, x.SectionId, x.Quantity, x.Was, x.Become, x.BalanceId, x.ReceiptId))
                .ToList());

        return Ok(model);
    }

    public record ReceiptSaveRequest(IReadOnlyList<ReceiptSaveItem> Items);

    public record ReceiptSaveItem(
        Guid PartId,
        Guid SectionId,
        int OpNumber,
        DateTime ReceiptDate,
        decimal Quantity,
        string? DocumentNumber,
        string? Comment);
}
