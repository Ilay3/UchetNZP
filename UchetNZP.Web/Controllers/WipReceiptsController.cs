using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Infrastructure;
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

        query = query.WhereMatchesLookup(search, x => x.Name, x => x.Code);

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

        query = query.WhereMatchesLookup(search, x => x.Name, x => x.Code);

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpGet("materials")]
    public async Task<IActionResult> GetMaterials(CancellationToken cancellationToken)
    {
        var items = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
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

    [HttpGet("labels/exists")]
    public async Task<IActionResult> LabelExists([FromQuery] string? labelNumber, [FromQuery] DateTime? receiptDate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(labelNumber))
        {
            return BadRequest("Не указан номер ярлыка.");
        }

        if (!receiptDate.HasValue)
        {
            return BadRequest("Не указана дата прихода.");
        }

        var normalizedLabelNumber = labelNumber.Trim();
        var year = receiptDate.Value.Date.Year;

        var exists = await _dbContext.WipLabels
            .AsNoTracking()
            .AnyAsync(x => x.Number == normalizedLabelNumber && x.LabelYear == year, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new { exists });
    }

    [HttpGet("material-stock")]
    public async Task<IActionResult> GetMaterialStock([FromQuery] Guid? materialId, CancellationToken cancellationToken)
    {
        if (!materialId.HasValue || materialId.Value == Guid.Empty)
        {
            return Ok(new MaterialStockResponseViewModel());
        }

        var material = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.Id == materialId.Value && x.IsActive)
            .Select(x => new { x.Id, x.UnitKind })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (material is null)
        {
            return Ok(new MaterialStockResponseViewModel());
        }

        var unitText = material.UnitKind == "SquareMeter" ? "м2" : "м";

        var baseQuery = _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Where(x => x.MetalMaterialId == material.Id);

        var unitsCount = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var sumSize = await baseQuery.SumAsync(x => (decimal?)x.SizeValue, cancellationToken).ConfigureAwait(false) ?? 0m;
        var sumWeight = await baseQuery.SumAsync(x => (decimal?)(x.Quantity > 0 ? x.TotalWeightKg / x.Quantity : x.TotalWeightKg), cancellationToken).ConfigureAwait(false) ?? 0m;

        var sample = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.ItemIndex)
            .Take(20)
            .Select(x => new MaterialStockUnitItemViewModel(
                x.GeneratedCode,
                x.SizeValue,
                string.IsNullOrWhiteSpace(x.SizeUnitText) ? unitText : x.SizeUnitText,
                x.Quantity > 0 ? x.TotalWeightKg / x.Quantity : x.TotalWeightKg,
                x.MetalReceipt != null ? x.MetalReceipt.ReceiptDate : x.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summary = new MaterialStockSummaryViewModel(
            unitText,
            unitsCount > 0 ? "Да" : "Нет",
            unitsCount,
            sumSize,
            sumWeight);

        return Ok(new MaterialStockResponseViewModel(summary, sample));
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
                x.MetalMaterialId,
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
                            x.IsAssigned,
                            x.VersionId))
                        .ToList());

        return Ok(model);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор прихода.");
        }

        var audits = await _dbContext.ReceiptAudits
            .AsNoTracking()
            .Where(audit => audit.ReceiptId == id)
            .OrderByDescending(audit => audit.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var labelIds = audits
            .SelectMany(audit => new[] { audit.PreviousLabelId, audit.NewLabelId })
            .Where(labelId => labelId.HasValue)
            .Select(labelId => labelId!.Value)
            .Distinct()
            .ToList();

        var labelLookup = labelIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.WipLabels
                .AsNoTracking()
                .Where(label => labelIds.Contains(label.Id))
                .ToDictionaryAsync(label => label.Id, label => label.Number, cancellationToken)
                .ConfigureAwait(false);

        var versions = audits
            .Select(audit =>
            {
                string? labelNumber = null;
                var labelId = audit.NewLabelId ?? audit.PreviousLabelId;

                if (labelId.HasValue)
                {
                    labelLookup.TryGetValue(labelId.Value, out labelNumber);
                }

                return new WipHistoryReceiptVersionViewModel(
                    audit.VersionId,
                    audit.Action,
                    audit.PreviousQuantity,
                    audit.NewQuantity,
                    ToLocalDateTime(audit.CreatedAt),
                    audit.Comment,
                    labelNumber,
                    audit.PreviousBalance,
                    audit.NewBalance);
            })
            .ToList();

        var response = new WipHistoryReceiptVersionsViewModel(id, versions);
        return Ok(response);
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
                result.Delta,
                result.VersionId);

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

    [HttpPost("{id:guid}/revert")]
    public async Task<IActionResult> Revert(Guid id, [FromBody] ReceiptRevertRequest? request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор прихода.");
        }

        if (request is null || request.VersionId == Guid.Empty)
        {
            return BadRequest("Версия отката не задана.");
        }

        try
        {
            var result = await _wipService.RevertReceiptAsync(id, request.VersionId, cancellationToken).ConfigureAwait(false);

            var viewModel = new ReceiptRevertResultViewModel(
                result.ReceiptId,
                result.BalanceId,
                result.PartId,
                result.SectionId,
                OperationNumber.Format(result.OpNumber),
                result.TargetQuantity,
                result.PreviousQuantity,
                result.NewQuantity,
                result.VersionId);

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
        Guid? MetalMaterialId,
        DateTime ReceiptDate,
        decimal Quantity,
        string? Comment,
        Guid? WipLabelId,
        string? LabelNumber,
        bool IsAssigned = false);

    private static DateTime ToLocalDateTime(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        var local = utcValue.ToLocalTime();
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public record ReceiptRevertRequest(Guid VersionId);
}
