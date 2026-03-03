using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Controllers;

[ApiController]
[Route("api/wip")]
public class WipApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public WipApiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("labels/by-number")]
    public async Task<IActionResult> GetLabelByNumber([FromQuery] Guid partId, [FromQuery] string number, CancellationToken ct)
    {
        var normalized = (number ?? string.Empty).Trim();
        var label = await _db.WipLabels.AsNoTracking()
            .Where(x => x.PartId == partId && x.Number == normalized)
            .Select(x => new
            {
                wipLabelId = x.Id,
                x.Number,
                x.Quantity,
                x.RemainingQuantity,
                currentSectionId = x.CurrentSectionId,
                currentOp = x.CurrentOpNumber,
                status = x.Status.ToString(),
            })
            .FirstOrDefaultAsync(ct);

        return label is null ? NotFound("Ярлык не найден.") : Ok(label);
    }

    [HttpGet("labels/search")]
    public async Task<IActionResult> SearchLabels([FromQuery] Guid partId, [FromQuery] string? q, [FromQuery] bool onlyWithRemaining = true, CancellationToken ct = default)
    {
        var term = q?.Trim() ?? string.Empty;
        var query = _db.WipLabels.AsNoTracking().Where(x => x.PartId == partId);
        if (onlyWithRemaining)
        {
            query = query.Where(x => x.RemainingQuantity > 0m);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x => x.Number.Contains(term));
        }

        var items = await query
            .OrderByDescending(x => x.LabelDate)
            .Take(50)
            .Select(x => new
            {
                id = x.Id,
                x.Number,
                x.RemainingQuantity,
                currentSectionId = x.CurrentSectionId,
                currentOp = x.CurrentOpNumber,
                x.LabelDate,
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    public record TransferPreviewRequest(Guid PartId, Guid FromSectionId, int FromOpNumber, Guid ToSectionId, int ToOpNumber, decimal Quantity, decimal ScrapQty, int? ScrapType, bool CreateResidualLabel, Guid? WipLabelId);

    [HttpPost("transfers/preview")]
    public async Task<IActionResult> PreviewTransfer([FromBody] TransferPreviewRequest req, CancellationToken ct)
    {
        var errors = new List<string>();
        if (req.Quantity <= 0m)
        {
            errors.Add("Количество должно быть больше нуля.");
        }

        WipLabel? label = null;
        if (req.WipLabelId.HasValue)
        {
            label = await _db.WipLabels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.WipLabelId.Value, ct);
            if (label is null)
            {
                errors.Add("Ярлык не найден.");
            }
            else if (label.PartId != req.PartId)
            {
                errors.Add("Ярлык относится к другой детали.");
            }
        }
        else if (req.CreateResidualLabel)
        {
            errors.Add("Для отрыва остатка нужно выбрать ярлык-источник.");
        }

        decimal remainingAfter = 0m;
        string? proposedResidual = null;
        if (label is not null)
        {
            remainingAfter = label.RemainingQuantity - req.Quantity - Math.Max(0m, req.ScrapQty);
            if (req.Quantity + Math.Max(0m, req.ScrapQty) > label.RemainingQuantity)
            {
                errors.Add("Количество передачи и брак превышают остаток ярлыка.");
            }

            if (req.CreateResidualLabel)
            {
                if (remainingAfter <= 0m)
                {
                    errors.Add("Нельзя выполнять отрыв ярлыка, если остаток на исходной операции равен 0.");
                }
                else
                {
                    var root = string.IsNullOrWhiteSpace(label.RootNumber)
                        ? WipLabelInvariants.ParseNumber(label.Number).RootNumber
                        : label.RootNumber;
                    var maxSuffix = await _db.WipLabels.AsNoTracking()
                        .Where(x => x.RootNumber == root)
                        .Select(x => x.Suffix)
                        .DefaultIfEmpty(0)
                        .MaxAsync(ct);
                    proposedResidual = WipLabelInvariants.FormatNumber(root, maxSuffix + 1);
                }
            }
        }

        return Ok(new
        {
            remainingAfter,
            canCreateResidual = req.CreateResidualLabel && remainingAfter > 0m && errors.Count == 0,
            proposedResidualLabelNumber = proposedResidual,
            errors,
        });
    }

    [HttpGet("labels/{id:guid}/card")]
    public async Task<IActionResult> GetLabelCard(Guid id, CancellationToken ct)
    {
        var label = await _db.WipLabels.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Quantity,
                x.RemainingQuantity,
                x.LabelDate,
                part = x.Part != null ? x.Part.Name : null,
                x.CurrentSectionId,
                x.CurrentOpNumber,
                status = x.Status.ToString(),
            })
            .FirstOrDefaultAsync(ct);

        if (label is null)
        {
            return NotFound("Ярлык не найден.");
        }

        var history = await _db.WipLabelLedger.AsNoTracking()
            .Where(x => x.FromLabelId == id || x.ToLabelId == id)
            .OrderByDescending(x => x.EventTime)
            .Take(30)
            .Select(x => new
            {
                date = x.EventTime,
                type = x.EventType.ToString(),
                document = x.RefEntityId,
                change = x.ToLabelId == id ? x.Qty : -x.Qty,
                comment = x.RefEntityType,
            })
            .ToListAsync(ct);

        return Ok(new { header = label, historyRows = history });
    }
}
