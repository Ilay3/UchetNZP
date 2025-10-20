using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Admin;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Shared;

namespace UchetNZP.Web.Controllers;

[Route("admin/wip")]
public class AdminWipController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IAdminWipService _adminWipService;

    public AdminWipController(AppDbContext dbContext, IAdminWipService adminWipService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _adminWipService = adminWipService ?? throw new ArgumentNullException(nameof(adminWipService));
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
                Text = string.IsNullOrWhiteSpace(x.Code) ? x.Name : string.Format(CultureInfo.CurrentCulture, "{0} — {1}", x.Code, x.Name),
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
                Text = string.IsNullOrWhiteSpace(x.Code) ? x.Name : string.Format(CultureInfo.CurrentCulture, "{0} — {1}", x.Code, x.Name),
                Selected = sectionId.HasValue && x.Id == sectionId.Value,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        sectionItems.Insert(0, new SelectListItem
        {
            Value = string.Empty,
            Text = "Все участки",
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

        var balances = await balancesQuery
            .OrderBy(x => x.Section!.Name)
            .ThenBy(x => x.Part!.Name)
            .ThenBy(x => x.OpNumber)
            .Select(x => new AdminWipBalanceRowViewModel
            {
                BalanceId = x.Id,
                PartId = x.PartId,
                SectionId = x.SectionId,
                PartDisplay = string.IsNullOrWhiteSpace(x.Part!.Code)
                    ? x.Part.Name
                    : string.Format(CultureInfo.CurrentCulture, "{0} — {1}", x.Part.Code, x.Part.Name),
                SectionDisplay = string.IsNullOrWhiteSpace(x.Section!.Code)
                    ? x.Section.Name
                    : string.Format(CultureInfo.CurrentCulture, "{0} — {1}", x.Section.Code, x.Section.Name),
                OpNumber = OperationNumber.Format(x.OpNumber),
                Quantity = x.Quantity,
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
            StatusMessage = statusMessage,
            ErrorMessage = errorMessage,
        };
    }
}
