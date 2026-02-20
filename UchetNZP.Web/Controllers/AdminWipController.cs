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

    public AdminWipController(AppDbContext dbContext, IAdminWipService adminWipService, IWipLabelLookupService labelLookupService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _adminWipService = adminWipService ?? throw new ArgumentNullException(nameof(adminWipService));
        _labelLookupService = labelLookupService ?? throw new ArgumentNullException(nameof(labelLookupService));
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

    [HttpPost("cleanup/preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewCleanup(AdminWipBulkCleanupInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminWipError"] = "Проверьте параметры массовой очистки.";
            return RedirectToAction(nameof(Index), new { partId = model.FilterPartId, sectionId = model.FilterSectionId, opNumber = model.FilterOpNumber });
        }

        try
        {
            var parsedOp = ParseOpNumber(model.FilterOpNumber);
            var preview = await _adminWipService.PreviewBulkCleanupAsync(
                    new AdminWipBulkCleanupRequestDto(model.FilterPartId, model.FilterSectionId, parsedOp, model.MinQuantity, model.Comment))
                .ConfigureAwait(false);

            TempData["AdminWipPendingCleanupJobId"] = preview.JobId.ToString();
            TempData["AdminWipPendingCleanupCount"] = preview.AffectedCount;
            TempData["AdminWipPendingCleanupQuantity"] = preview.AffectedQuantity.ToString(CultureInfo.InvariantCulture);
            TempData["AdminWipMessage"] = $"Dry-run: будет обнулено {preview.AffectedCount} строк, суммарно {preview.AffectedQuantity:0.###}. Подтвердите выполнение.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["AdminWipError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { partId = model.FilterPartId, sectionId = model.FilterSectionId, opNumber = model.FilterOpNumber });
    }

    [HttpPost("cleanup/execute")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteCleanup(AdminWipBulkCleanupExecuteInputModel model)
    {
        try
        {
            var result = await _adminWipService.ExecuteBulkCleanupAsync(new AdminWipBulkCleanupExecuteDto(model.JobId, model.Confirmed)).ConfigureAwait(false);
            TempData["AdminWipMessage"] = $"Массовая очистка выполнена. JobId: {result.JobId}. Обновлено строк: {result.UpdatedCount}, количество: {result.UpdatedQuantity:0.###}.";
            TempData.Remove("AdminWipPendingCleanupJobId");
            TempData.Remove("AdminWipPendingCleanupCount");
            TempData.Remove("AdminWipPendingCleanupQuantity");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["AdminWipError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { partId = model.FilterPartId, sectionId = model.FilterSectionId, opNumber = model.FilterOpNumber });
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

    private static int? ParseOpNumber(string? opNumber)
    {
        if (string.IsNullOrWhiteSpace(opNumber) || !OperationNumber.TryParse(opNumber, out var parsed))
        {
            return null;
        }

        return parsed;
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
            BulkCleanup = new AdminWipBulkCleanupInputModel
            {
                FilterPartId = partId,
                FilterSectionId = sectionId,
                FilterOpNumber = parsedOpNumber.HasValue
                    ? OperationNumber.Format(parsedOpNumber.Value)
                    : OperationNumber.Normalize(opNumber),
            },
            PendingCleanup = GetPendingCleanup(),
        };
    }

    private AdminWipBulkCleanupPreviewViewModel? GetPendingCleanup()
    {
        var jobIdRaw = TempData.Peek("AdminWipPendingCleanupJobId") as string;
        var countRaw = TempData.Peek("AdminWipPendingCleanupCount");
        var quantityRaw = TempData.Peek("AdminWipPendingCleanupQuantity") as string;

        if (!Guid.TryParse(jobIdRaw, out var jobId))
        {
            return null;
        }

        var count = 0;
        if (countRaw is int countInt)
        {
            count = countInt;
        }
        else if (countRaw is string countString)
        {
            int.TryParse(countString, out count);
        }

        var quantity = 0m;
        if (!string.IsNullOrWhiteSpace(quantityRaw))
        {
            decimal.TryParse(quantityRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out quantity);
        }

        return new AdminWipBulkCleanupPreviewViewModel
        {
            JobId = jobId,
            AffectedCount = count,
            AffectedQuantity = quantity,
        };
    }
}
