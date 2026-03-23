using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Admin;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
using UchetNZP.Shared;

namespace UchetNZP.Web.Controllers;

[Route("admin/wip")]
public class AdminWipController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IAdminWipService _adminWipService;
    private readonly IWipLabelLookupService _labelLookupService;
    private readonly IWipLabelService _wipLabelService;

    public AdminWipController(
        AppDbContext dbContext,
        IAdminWipService adminWipService,
        IWipLabelLookupService labelLookupService,
        IWipLabelService wipLabelService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _adminWipService = adminWipService ?? throw new ArgumentNullException(nameof(adminWipService));
        _labelLookupService = labelLookupService ?? throw new ArgumentNullException(nameof(labelLookupService));
        _wipLabelService = wipLabelService ?? throw new ArgumentNullException(nameof(wipLabelService));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? partId, Guid? sectionId, string? opNumber)
    {
        var statusMessage = TempData["AdminWipMessage"] as string;
        var errorMessage = TempData["AdminWipError"] as string;

        var model = await BuildIndexViewModelAsync(partId, sectionId, opNumber, statusMessage, errorMessage).ConfigureAwait(false);
        return View(model);
    }

    [HttpPost("adjust")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(AdminWipAdjustmentInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminWipError"] = "Проверьте корректность введённых данных.";
            return RedirectToIndex(model);
        }

        try
        {
            var request = new AdminWipAdjustmentRequestDto(model.BalanceId, model.NewQuantity, model.Comment);
            var result = await _adminWipService.AdjustBalanceAsync(request).ConfigureAwait(false);

            if (result.AdjustmentId == Guid.Empty)
            {
                TempData["AdminWipMessage"] = "Количество не изменилось. Запись оставлена без изменений.";
            }
            else
            {
                TempData["AdminWipMessage"] = string.Format(
                    CultureInfo.CurrentCulture,
                    "Остаток обновлён: было {0:0.###}, стало {1:0.###} (Δ {2:+0.###;-0.###;0}).",
                    result.PreviousQuantity,
                    result.NewQuantity,
                    result.Delta);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["AdminWipError"] = ex.Message;
        }

        return RedirectToIndex(model);
    }

    [HttpPost("delete-label")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLabel(AdminWipDeleteLabelInputModel model)
    {
        if (!ModelState.IsValid || model.LabelId == Guid.Empty)
        {
            TempData["AdminWipError"] = "Не удалось определить ярлык для удаления.";
            return RedirectToIndex(model);
        }

        try
        {
            var labelNumber = await _dbContext.WipLabels
                .AsNoTracking()
                .Where(x => x.Id == model.LabelId)
                .Select(x => x.Number)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            await _wipLabelService.DeleteLabelAsync(model.LabelId).ConfigureAwait(false);

            TempData["AdminWipMessage"] = string.IsNullOrWhiteSpace(labelNumber)
                ? "Ярлык удалён из системы."
                : $"Ярлык {labelNumber} удалён из системы.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["AdminWipError"] = ex.Message;
        }

        return RedirectToIndex(model);
    }

    private RedirectToActionResult RedirectToIndex(AdminWipAdjustmentInputModel model)
    {
        return RedirectToAction(
            nameof(Index),
            new
            {
                partId = model.FilterPartId,
                sectionId = model.FilterSectionId,
                opNumber = model.FilterOpNumber,
            });
    }

    private RedirectToActionResult RedirectToIndex(AdminWipDeleteLabelInputModel model)
    {
        return RedirectToAction(
            nameof(Index),
            new
            {
                partId = model.FilterPartId,
                sectionId = model.FilterSectionId,
                opNumber = model.FilterOpNumber,
            });
    }

    private async Task<AdminWipIndexViewModel> BuildIndexViewModelAsync(
        Guid? partId,
        Guid? sectionId,
        string? opNumber,
        string? statusMessage,
        string? errorMessage)
    {
        int? parsedOpNumber = null;
        if (!string.IsNullOrWhiteSpace(opNumber) && OperationNumber.TryParse(opNumber, out var parsed))
        {
            parsedOpNumber = parsed;
        }

        var partItems = await _dbContext.Parts
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = NameWithCodeFormatter.getNameWithCode(x.Name, x.Code),
                Selected = partId.HasValue && x.Id == partId.Value,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        partItems.Insert(0, new SelectListItem
        {
            Value = string.Empty,
            Text = "Все детали",
            Selected = !partId.HasValue,
        });

        var sectionItems = await _dbContext.Sections
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = NameWithCodeFormatter.getNameWithCode(x.Name, x.Code),
                Selected = sectionId.HasValue && x.Id == sectionId.Value,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        sectionItems.Insert(0, new SelectListItem
        {
            Value = string.Empty,
            Text = "Все виды работ",
            Selected = !sectionId.HasValue,
        });

        var balancesQuery = _dbContext.WipBalances
            .AsNoTracking()
            .Include(x => x.Part)
            .Include(x => x.Section)
            .AsQueryable();

        if (partId.HasValue)
        {
            balancesQuery = balancesQuery.Where(x => x.PartId == partId.Value);
        }

        if (sectionId.HasValue)
        {
            balancesQuery = balancesQuery.Where(x => x.SectionId == sectionId.Value);
        }

        if (parsedOpNumber.HasValue)
        {
            balancesQuery = balancesQuery.Where(x => x.OpNumber == parsedOpNumber.Value);
        }

        var rawBalances = await balancesQuery
            .OrderBy(x => x.Section!.Name)
            .ThenBy(x => x.Part!.Name)
            .ThenBy(x => x.OpNumber)
            .Select(x => new
            {
                x.Id,
                x.PartId,
                x.SectionId,
                x.OpNumber,
                x.Quantity,
                PartName = x.Part!.Name,
                PartCode = x.Part.Code,
                SectionName = x.Section!.Name,
                SectionCode = x.Section.Code,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        var labelKeys = rawBalances
            .Select(x => new LabelLookupKey(x.PartId, x.SectionId, x.OpNumber))
            .ToList();

        var labelLookup = await _labelLookupService
            .LoadAsync(labelKeys, CancellationToken.None, null, DateTime.UtcNow.AddDays(1))
            .ConfigureAwait(false);

        var balances = rawBalances
            .Select(x =>
            {
                var partDisplay = NameWithCodeFormatter.getNameWithCode(x.PartName, x.PartCode);
                var sectionDisplay = NameWithCodeFormatter.getNameWithCode(x.SectionName, x.SectionCode);
                var labels = _labelLookupService.GetAllLabels(
                    labelLookup,
                    new LabelLookupKey(x.PartId, x.SectionId, x.OpNumber));

                return new AdminWipBalanceRowViewModel
                {
                    BalanceId = x.Id,
                    PartId = x.PartId,
                    SectionId = x.SectionId,
                    PartDisplay = partDisplay,
                    SectionDisplay = sectionDisplay,
                    OpNumber = OperationNumber.Format(x.OpNumber),
                    Quantity = x.Quantity,
                    LabelNumbers = labels,
                };
            })
            .ToList();

        var labelsQuery = _dbContext.WipLabels
            .AsNoTracking()
            .Include(x => x.Part)
            .AsQueryable();

        if (partId.HasValue)
        {
            labelsQuery = labelsQuery.Where(x => x.PartId == partId.Value);
        }

        if (sectionId.HasValue)
        {
            labelsQuery = labelsQuery.Where(x => x.CurrentSectionId == sectionId.Value);
        }

        if (parsedOpNumber.HasValue)
        {
            labelsQuery = labelsQuery.Where(x => x.CurrentOpNumber == parsedOpNumber.Value);
        }

        var labels = await labelsQuery
            .OrderByDescending(x => x.LabelDate)
            .ThenBy(x => x.Number)
            .Select(x => new AdminWipLabelRowViewModel
            {
                LabelId = x.Id,
                Number = x.Number,
                PartDisplay = NameWithCodeFormatter.getNameWithCode(x.Part!.Name, x.Part.Code),
                CurrentSectionDisplay = x.CurrentSectionId.HasValue
                    ? _dbContext.Sections
                        .Where(section => section.Id == x.CurrentSectionId.Value)
                        .Select(section => NameWithCodeFormatter.getNameWithCode(section.Name, section.Code))
                        .FirstOrDefault()
                    : null,
                CurrentOpNumber = x.CurrentOpNumber.HasValue
                    ? OperationNumber.Format(x.CurrentOpNumber.Value)
                    : null,
                Status = x.Status.ToString(),
                Quantity = x.Quantity,
                RemainingQuantity = x.RemainingQuantity,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return new AdminWipIndexViewModel
        {
            SelectedPartId = partId,
            SelectedSectionId = sectionId,
            SelectedOpNumber = parsedOpNumber.HasValue
                ? OperationNumber.Format(parsedOpNumber.Value)
                : OperationNumber.Normalize(opNumber),
            Parts = partItems,
            Sections = sectionItems,
            Balances = balances,
            Labels = labels,
            StatusMessage = statusMessage,
            ErrorMessage = errorMessage,
        };
    }
}
