using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Admin;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Authorize]
[Route("admin")]
public class AdminController : Controller
{
    private const string StatusTempDataKey = "AdminStatus";
    private const string ErrorTempDataKey = "AdminError";

    private readonly IAdminCatalogService _catalogService;

    public AdminController(IAdminCatalogService catalogService)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? partSearch,
        string? operationSearch,
        string? sectionSearch,
        Guid? wipBalancePartFilter,
        Guid? wipBalanceSectionFilter,
        string? wipBalanceSearch,
        CancellationToken cancellationToken)
    {
        var statusMessage = ReadTempData(StatusTempDataKey);
        var errorMessage = ReadTempData(ErrorTempDataKey);

        var filters = NormalizeFilters(new AdminCatalogFilters(
            partSearch,
            operationSearch,
            sectionSearch,
            wipBalancePartFilter,
            wipBalanceSectionFilter,
            wipBalanceSearch));

        var viewModel = await BuildIndexViewModelAsync(
            cancellationToken,
            filters,
            statusMessage: statusMessage,
            errorMessage: errorMessage).ConfigureAwait(false);

        return View(viewModel);
    }

    [HttpPost("parts/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePart(AdminPartInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                partInput: input,
                errorMessage: "Исправьте ошибки формы создания детали.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.CreatePartAsync(new AdminPartEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Деталь успешно создана.";
            var filters = ReadFilters();
            return RedirectToAction(nameof(Index), BuildRouteValues(filters));
        }
        catch (Exception ex)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                partInput: input,
                errorMessage: ex.Message).ConfigureAwait(false);

            return View("Index", viewModel);
        }
    }

    [HttpPost("parts/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePart(AdminPartUpdateInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                errorMessage: "Исправьте данные для редактирования детали.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.UpdatePartAsync(input.Id, new AdminPartEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Изменения детали сохранены.";
            var filters = ReadFilters();
            return RedirectToAction(nameof(Index), BuildRouteValues(filters));
        }
        catch (Exception ex)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                errorMessage: ex.Message).ConfigureAwait(false);

            return View("Index", viewModel);
        }
    }

    [HttpPost("parts/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePart(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _catalogService.DeletePartAsync(id, cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Деталь удалена.";
        }
        catch (Exception ex)
        {
            TempData[ErrorTempDataKey] = ex.Message;
        }

        var filters = ReadFilters();
        return RedirectToAction(nameof(Index), BuildRouteValues(filters));
    }

    [HttpPost("operations/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOperation(AdminOperationInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                operationInput: input,
                errorMessage: "Исправьте ошибки формы создания операции.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.CreateOperationAsync(new AdminOperationEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Операция успешно создана.";
            var filters = ReadFilters();
            return RedirectToAction(nameof(Index), BuildRouteValues(filters));
        }
        catch (Exception ex)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                operationInput: input,
                errorMessage: ex.Message).ConfigureAwait(false);

            return View("Index", viewModel);
        }
    }

    [HttpPost("operations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOperation(AdminOperationUpdateInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                errorMessage: "Исправьте данные для редактирования операции.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.UpdateOperationAsync(input.Id, new AdminOperationEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Изменения операции сохранены.";
            var filters = ReadFilters();
            return RedirectToAction(nameof(Index), BuildRouteValues(filters));
        }
        catch (Exception ex)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                errorMessage: ex.Message).ConfigureAwait(false);

            return View("Index", viewModel);
        }
    }

    [HttpPost("operations/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOperation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _catalogService.DeleteOperationAsync(id, cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Операция удалена.";
        }
        catch (Exception ex)
        {
            TempData[ErrorTempDataKey] = ex.Message;
        }

        var filters = ReadFilters();
        return RedirectToAction(nameof(Index), BuildRouteValues(filters));
    }

    [HttpPost("sections/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSection(AdminSectionInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                sectionInput: input,
                errorMessage: "Исправьте ошибки формы создания участка.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.CreateSectionAsync(new AdminSectionEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Участок успешно создан.";
            var filters = ReadFilters();
            return RedirectToAction(nameof(Index), BuildRouteValues(filters));
        }
        catch (Exception ex)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                sectionInput: input,
                errorMessage: ex.Message).ConfigureAwait(false);

            return View("Index", viewModel);
        }
    }

    [HttpPost("sections/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSection(AdminSectionUpdateInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                errorMessage: "Исправьте данные для редактирования участка.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.UpdateSectionAsync(input.Id, new AdminSectionEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Изменения участка сохранены.";
            var filters = ReadFilters();
            return RedirectToAction(nameof(Index), BuildRouteValues(filters));
        }
        catch (Exception ex)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                errorMessage: ex.Message).ConfigureAwait(false);

            return View("Index", viewModel);
        }
    }

    [HttpPost("sections/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSection(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _catalogService.DeleteSectionAsync(id, cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Участок удален.";
        }
        catch (Exception ex)
        {
            TempData[ErrorTempDataKey] = ex.Message;
        }

        var filters = ReadFilters();
        return RedirectToAction(nameof(Index), BuildRouteValues(filters));
    }

    [HttpPost("balances/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWipBalance(AdminWipBalanceInputModel input, CancellationToken cancellationToken)
    {
        if (input.PartId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(AdminWipBalanceInputModel.PartId), "Выберите деталь.");
        }

        if (input.SectionId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(AdminWipBalanceInputModel.SectionId), "Выберите участок.");
        }

        if (!ModelState.IsValid)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                wipBalanceInput: input,
                errorMessage: "Исправьте ошибки формы создания остатка.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.CreateWipBalanceAsync(
                new AdminWipBalanceEditDto(input.PartId, input.SectionId, input.OpNumber, input.Quantity),
                cancellationToken).ConfigureAwait(false);

            TempData[StatusTempDataKey] = "Остаток успешно создан.";
            var filters = ReadFilters();
            return RedirectToAction(nameof(Index), BuildRouteValues(filters));
        }
        catch (Exception ex)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                wipBalanceInput: input,
                errorMessage: ex.Message).ConfigureAwait(false);

            return View("Index", viewModel);
        }
    }

    [HttpPost("balances/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWipBalance(AdminWipBalanceUpdateInputModel input, CancellationToken cancellationToken)
    {
        if (input.Id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(AdminWipBalanceUpdateInputModel.Id), "Неизвестный остаток.");
        }

        if (input.PartId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(AdminWipBalanceUpdateInputModel.PartId), "Выберите деталь.");
        }

        if (input.SectionId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(AdminWipBalanceUpdateInputModel.SectionId), "Выберите участок.");
        }

        if (!ModelState.IsValid)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                errorMessage: "Исправьте данные для редактирования остатка.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.UpdateWipBalanceAsync(
                input.Id,
                new AdminWipBalanceEditDto(input.PartId, input.SectionId, input.OpNumber, input.Quantity),
                cancellationToken).ConfigureAwait(false);

            TempData[StatusTempDataKey] = "Изменения остатка сохранены.";
            var filters = ReadFilters();
            return RedirectToAction(nameof(Index), BuildRouteValues(filters));
        }
        catch (Exception ex)
        {
            var filters = ReadFilters();
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                filters,
                errorMessage: ex.Message).ConfigureAwait(false);

            return View("Index", viewModel);
        }
    }

    [HttpPost("balances/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWipBalance(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _catalogService.DeleteWipBalanceAsync(id, cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Остаток удален.";
        }
        catch (Exception ex)
        {
            TempData[ErrorTempDataKey] = ex.Message;
        }

        var filters = ReadFilters();
        return RedirectToAction(nameof(Index), BuildRouteValues(filters));
    }

    private async Task<AdminIndexViewModel> BuildIndexViewModelAsync(
        CancellationToken cancellationToken,
        AdminCatalogFilters? filters = null,
        AdminPartInputModel? partInput = null,
        AdminOperationInputModel? operationInput = null,
        AdminSectionInputModel? sectionInput = null,
        AdminWipBalanceInputModel? wipBalanceInput = null,
        string? statusMessage = null,
        string? errorMessage = null)
    {
        var normalizedFilters = NormalizeFilters(filters ?? new AdminCatalogFilters());

        var partDtos = await _catalogService.GetPartsAsync(cancellationToken).ConfigureAwait(false);
        var operationDtos = await _catalogService.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        var sectionDtos = await _catalogService.GetSectionsAsync(cancellationToken).ConfigureAwait(false);
        var balanceDtos = await _catalogService.GetWipBalancesAsync(cancellationToken).ConfigureAwait(false);

        var partRows = partDtos
            .Select(x => new AdminEntityRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
            })
            .ToList();

        var parts = FilterEntities(partRows, normalizedFilters.PartSearch);

        var operationRows = operationDtos
            .Select(x => new AdminEntityRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
            })
            .ToList();

        var operations = FilterEntities(operationRows, normalizedFilters.OperationSearch);

        var sectionRows = sectionDtos
            .Select(x => new AdminEntityRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
            })
            .ToList();

        var sections = FilterEntities(sectionRows, normalizedFilters.SectionSearch);

        var balances = balanceDtos
            .Select(x => new AdminCatalogWipBalanceRowViewModel
            {
                Id = x.Id,
                PartId = x.PartId,
                PartName = x.PartName,
                SectionId = x.SectionId,
                SectionName = x.SectionName,
                OpNumber = x.OpNumber,
                Quantity = x.Quantity,
            })
            .ToList();

        if (normalizedFilters.WipBalancePartFilter.HasValue)
        {
            var partId = normalizedFilters.WipBalancePartFilter.Value;
            balances = balances
                .Where(x => x.PartId == partId)
                .ToList();
        }

        if (normalizedFilters.WipBalanceSectionFilter.HasValue)
        {
            var sectionId = normalizedFilters.WipBalanceSectionFilter.Value;
            balances = balances
                .Where(x => x.SectionId == sectionId)
                .ToList();
        }

        if (!string.IsNullOrEmpty(normalizedFilters.WipBalanceSearch))
        {
            var search = normalizedFilters.WipBalanceSearch;
            balances = balances
                .Where(x =>
                    ContainsText(x.PartName, search)
                    || ContainsText(x.SectionName, search)
                    || x.OpNumber.ToString(CultureInfo.CurrentCulture).Contains(search, StringComparison.CurrentCultureIgnoreCase)
                    || x.Quantity.ToString(CultureInfo.CurrentCulture).Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        }

        var partOptions = partRows
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = FormatEntityDisplay(x.Name, x.Code),
            })
            .ToList();

        var sectionOptions = sectionRows
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = FormatEntityDisplay(x.Name, x.Code),
            })
            .ToList();

        return new AdminIndexViewModel
        {
            Parts = parts,
            Operations = operations,
            Sections = sections,
            WipBalances = balances,
            PartOptions = partOptions,
            SectionOptions = sectionOptions,
            PartInput = partInput ?? new AdminPartInputModel(),
            OperationInput = operationInput ?? new AdminOperationInputModel(),
            SectionInput = sectionInput ?? new AdminSectionInputModel(),
            WipBalanceInput = wipBalanceInput ?? new AdminWipBalanceInputModel(),
            PartSearch = normalizedFilters.PartSearch,
            OperationSearch = normalizedFilters.OperationSearch,
            SectionSearch = normalizedFilters.SectionSearch,
            WipBalancePartFilter = normalizedFilters.WipBalancePartFilter,
            WipBalanceSectionFilter = normalizedFilters.WipBalanceSectionFilter,
            WipBalanceSearch = normalizedFilters.WipBalanceSearch,
            StatusMessage = NormalizeMessage(statusMessage),
            ErrorMessage = NormalizeMessage(errorMessage),
        };
    }

    private static string? NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return message;
    }

    private string? ReadTempData(string key)
    {
        if (TempData.TryGetValue(key, out var value) && value is string message)
        {
            return message;
        }

        return null;
    }

    private static string FormatEntityDisplay(string name, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return name;
        }

        return string.Format(CultureInfo.CurrentCulture, "{0} ({1})", name, code);
    }

    private AdminCatalogFilters ReadFilters()
    {
        var filters = new AdminCatalogFilters();

        var query = Request?.Query;
        if (query is not null)
        {
            filters = filters with
            {
                PartSearch = GetQueryValue(query, "partSearch"),
                OperationSearch = GetQueryValue(query, "operationSearch"),
                SectionSearch = GetQueryValue(query, "sectionSearch"),
                WipBalancePartFilter = GetGuidFromText(GetQueryValue(query, "wipBalancePartFilter")),
                WipBalanceSectionFilter = GetGuidFromText(GetQueryValue(query, "wipBalanceSectionFilter")),
                WipBalanceSearch = GetQueryValue(query, "wipBalanceSearch"),
            };
        }

        if (Request?.HasFormContentType == true)
        {
            var form = Request?.Form;
            if (form is not null)
            {
                filters = filters with
                {
                    PartSearch = GetFormValue(form, "partSearch") ?? filters.PartSearch,
                    OperationSearch = GetFormValue(form, "operationSearch") ?? filters.OperationSearch,
                    SectionSearch = GetFormValue(form, "sectionSearch") ?? filters.SectionSearch,
                    WipBalancePartFilter = GetGuidFromText(GetFormValue(form, "wipBalancePartFilter")) ?? filters.WipBalancePartFilter,
                    WipBalanceSectionFilter = GetGuidFromText(GetFormValue(form, "wipBalanceSectionFilter")) ?? filters.WipBalanceSectionFilter,
                    WipBalanceSearch = GetFormValue(form, "wipBalanceSearch") ?? filters.WipBalanceSearch,
                };
            }
        }

        var ret = NormalizeFilters(filters);
        return ret;
    }

    private static string? GetQueryValue(IQueryCollection collection, string key)
    {
        string? ret = null;
        if (collection.TryGetValue(key, out var values))
        {
            var candidate = values.ToString();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                ret = candidate;
            }
        }

        return ret;
    }

    private static string? GetFormValue(IFormCollection collection, string key)
    {
        string? ret = null;
        if (collection.TryGetValue(key, out var values))
        {
            var candidate = values.ToString();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                ret = candidate;
            }
        }

        return ret;
    }

    private static Guid? GetGuidFromText(string? text)
    {
        Guid? ret = null;
        if (!string.IsNullOrWhiteSpace(text) && Guid.TryParse(text, out var parsed))
        {
            ret = parsed;
        }

        return ret;
    }

    private static AdminCatalogFilters NormalizeFilters(AdminCatalogFilters filters)
    {
        var ret = filters with
        {
            PartSearch = NormalizeSearchValue(filters.PartSearch),
            OperationSearch = NormalizeSearchValue(filters.OperationSearch),
            SectionSearch = NormalizeSearchValue(filters.SectionSearch),
            WipBalanceSearch = NormalizeSearchValue(filters.WipBalanceSearch),
        };

        return ret;
    }

    private static string? NormalizeSearchValue(string? value)
    {
        string? ret = null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            ret = value.Trim();
        }

        return ret;
    }

    private static List<AdminEntityRowViewModel> FilterEntities(
        List<AdminEntityRowViewModel> source,
        string? search)
    {
        if (string.IsNullOrEmpty(search))
        {
            return source;
        }

        var ret = source
            .Where(x => ContainsText(x.Name, search) || (!string.IsNullOrEmpty(x.Code) && ContainsText(x.Code!, search)))
            .ToList();

        return ret;
    }

    private static bool ContainsText(string value, string search)
        => value.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private static RouteValueDictionary BuildRouteValues(AdminCatalogFilters filters)
    {
        var ret = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(filters.PartSearch))
        {
            ret["partSearch"] = filters.PartSearch!;
        }

        if (!string.IsNullOrEmpty(filters.OperationSearch))
        {
            ret["operationSearch"] = filters.OperationSearch!;
        }

        if (!string.IsNullOrEmpty(filters.SectionSearch))
        {
            ret["sectionSearch"] = filters.SectionSearch!;
        }

        if (filters.WipBalancePartFilter.HasValue)
        {
            ret["wipBalancePartFilter"] = filters.WipBalancePartFilter.Value;
        }

        if (filters.WipBalanceSectionFilter.HasValue)
        {
            ret["wipBalanceSectionFilter"] = filters.WipBalanceSectionFilter.Value;
        }

        if (!string.IsNullOrEmpty(filters.WipBalanceSearch))
        {
            ret["wipBalanceSearch"] = filters.WipBalanceSearch!;
        }

        return ret;
    }

    private sealed record AdminCatalogFilters(
        string? PartSearch = null,
        string? OperationSearch = null,
        string? SectionSearch = null,
        Guid? WipBalancePartFilter = null,
        Guid? WipBalanceSectionFilter = null,
        string? WipBalanceSearch = null);
}
