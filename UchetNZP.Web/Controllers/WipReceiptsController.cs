using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Shared;

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
                OperationNumber.Format(x.OpNumber),
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
    public async Task<IActionResult> GetBalance([FromQuery] Guid partId, [FromQuery] Guid sectionId, [FromQuery] string? opNumber, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty || sectionId == Guid.Empty || string.IsNullOrWhiteSpace(opNumber))
        {
            return BadRequest("Недостаточно данных для определения остатка.");
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

        var balance = await _dbContext.WipBalances
            .AsNoTracking()
            .Where(x => x.PartId == partId && x.SectionId == sectionId && x.OpNumber == parsedOpNumber)
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

        List<ReceiptItemDto> dtos;
        try
        {
            dtos = request.Items.Select(x => new ReceiptItemDto(
                x.PartId,
                OperationNumber.Parse(x.OpNumber, nameof(ReceiptSaveItem.OpNumber)),
                x.SectionId,
                x.ReceiptDate,
                x.Quantity,
                x.Comment,
                x.WipLabelId,
                x.LabelNumber,
                x.IsAssigned)).ToList();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        ReceiptBatchSummaryDto summary;
        try
        {
            summary = await _wipService.AddReceiptsBatchAsync(dtos, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var model = new ReceiptBatchSummaryViewModel(
            summary.Saved,
            summary.Items
                .Select(x => new ReceiptSummaryItemViewModel(
                    x.PartId,
                    OperationNumber.Format(x.OpNumber),
                    x.SectionId,
                    x.Quantity,
                    x.Was,
                    x.Become,
                    x.BalanceId,
                    x.ReceiptId,
                    x.WipLabelId,
                    x.LabelNumber,
                    x.IsAssigned))
                .ToList());

        return Ok(model);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор прихода.");
        }

        try
        {
            var result = await _wipService.DeleteReceiptAsync(id, cancellationToken).ConfigureAwait(false);

            var viewModel = new ReceiptDeleteResultViewModel(
                result.ReceiptId,
                result.BalanceId,
                result.PartId,
                result.SectionId,
                OperationNumber.Format(result.OpNumber),
                result.ReceiptQuantity,
                result.PreviousQuantity,
                result.RestoredQuantity,
                result.Delta);

            return Ok(viewModel);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public record ReceiptSaveRequest(IReadOnlyList<ReceiptSaveItem> Items);

    public record ReceiptSaveItem(
        Guid PartId,
        Guid SectionId,
        string OpNumber,
        DateTime ReceiptDate,
        decimal Quantity,
        string? Comment,
        Guid? WipLabelId,
        string? LabelNumber,
        bool IsAssigned = false);
}
