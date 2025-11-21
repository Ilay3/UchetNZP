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
using UchetNZP.Shared;
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
            PartSearch: partSearch,
            OperationSearch: operationSearch,
            SectionSearch: sectionSearch,
            WipBalancePartFilter: wipBalancePartFilter,
            WipBalanceSectionFilter: wipBalanceSectionFilter,
            WipBalanceSearch: wipBalanceSearch));

        var viewModel = await BuildIndexViewModelAsync(
            cancellationToken,
            filters,
            statusMessage: statusMessage,
            errorMessage: errorMessage).ConfigureAwait(false);

        return View(viewModel);
    }

    [HttpGet("parts/data")]
    public async Task<IActionResult> GetPartsData(
        string? partSearch,
        string? sort,
        string? order,
        int offset = 0,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var filters = NormalizeFilters(new AdminCatalogFilters(PartSearch: partSearch));
        var partDtos = await _catalogService.GetPartsAsync(cancellationToken).ConfigureAwait(false);
        var partRows = partDtos
            .Select(x => new AdminEntityRowViewModel { Id = x.Id, Name = x.Name, Code = x.Code })
            .ToList();

        var filtered = FilterEntities(partRows, filters.PartSearch);
        var total = filtered.Count;
        var sorted = SortEntities(filtered, sort, order)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return Json(new { total, rows = sorted });
    }

    [HttpGet("operations/data")]
    public async Task<IActionResult> GetOperationsData(
        string? operationSearch,
        string? sort,
        string? order,
        int offset = 0,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var filters = NormalizeFilters(new AdminCatalogFilters(OperationSearch: operationSearch));
        var operationDtos = await _catalogService.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        var operationRows = operationDtos
            .Select(x => new AdminEntityRowViewModel { Id = x.Id, Name = x.Name, Code = x.Code })
            .ToList();

        var filtered = FilterEntities(operationRows, filters.OperationSearch);
        var total = filtered.Count;
        var sorted = SortEntities(filtered, sort, order)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return Json(new { total, rows = sorted });
    }

    [HttpGet("sections/data")]
    public async Task<IActionResult> GetSectionsData(
        string? sectionSearch,
        string? sort,
        string? order,
        int offset = 0,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var filters = NormalizeFilters(new AdminCatalogFilters(SectionSearch: sectionSearch));
        var sectionDtos = await _catalogService.GetSectionsAsync(cancellationToken).ConfigureAwait(false);
        var sectionRows = sectionDtos
            .Select(x => new AdminEntityRowViewModel { Id = x.Id, Name = x.Name, Code = x.Code })
            .ToList();

        var filtered = FilterEntities(sectionRows, filters.SectionSearch);
        var total = filtered.Count;
        var sorted = SortEntities(filtered, sort, order)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return Json(new { total, rows = sorted });
    }

    [HttpGet("balances/data")]
    public async Task<IActionResult> GetBalancesData(
        Guid? wipBalancePartFilter,
        Guid? wipBalanceSectionFilter,
        string? wipBalanceSearch,
        string? sort,
        string? order,
        int offset = 0,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var filters = NormalizeFilters(new AdminCatalogFilters(
            WipBalancePartFilter: wipBalancePartFilter,
            WipBalanceSectionFilter: wipBalanceSectionFilter,
            WipBalanceSearch: wipBalanceSearch));

        var balanceDtos = await _catalogService.GetWipBalancesAsync(cancellationToken).ConfigureAwait(false);
        var balances = balanceDtos
            .Select(x => new AdminCatalogWipBalanceRowViewModel
            {
                Id = x.Id,
                PartId = x.PartId,
                PartName = x.PartName,
                SectionId = x.SectionId,
                SectionName = x.SectionName,
                OperationId = x.OperationId,
                OperationName = x.OperationName,
                OperationLabel = x.OperationLabel,
                OpNumber = x.OpNumber,
                OpNumberFormatted = OperationNumber.Format(x.OpNumber),
                Quantity = x.Quantity,
            })
            .ToList();

        if (filters.WipBalancePartFilter.HasValue)
        {
            balances = balances
                .Where(x => x.PartId == filters.WipBalancePartFilter.Value)
                .ToList();
        }

        if (filters.WipBalanceSectionFilter.HasValue)
        {
            balances = balances
                .Where(x => x.SectionId == filters.WipBalanceSectionFilter.Value)
                .ToList();
        }

        if (!string.IsNullOrEmpty(filters.WipBalanceSearch))
        {
            var search = filters.WipBalanceSearch;
            balances = balances
                .Where(x =>
                    ContainsText(x.PartName, search)
                    || ContainsText(x.SectionName, search)
                    || ContainsText(x.OperationName, search)
                    || (!string.IsNullOrEmpty(x.OperationLabel) && ContainsText(x.OperationLabel!, search))
                    || x.OpNumberFormatted.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                    || x.Quantity.ToString(CultureInfo.CurrentCulture).Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        }

        var total = balances.Count;
        var sorted = SortBalances(balances, sort, order)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return Json(new { total, rows = sorted });
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
                new AdminWipBalanceEditDto(
                    input.PartId,
                    input.SectionId,
                    ParseOperationNumber(input.OpNumber, nameof(AdminWipBalanceInputModel.OpNumber)),
                    input.Quantity,
                    input.OperationId,
                    input.OperationLabel),
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
                new AdminWipBalanceEditDto(
                    input.PartId,
                    input.SectionId,
                    ParseOperationNumber(input.OpNumber, nameof(AdminWipBalanceInputModel.OpNumber)),
                    input.Quantity,
                    input.OperationId,
                    input.OperationLabel),
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

    [HttpPost("api/parts")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePartApi([FromBody] AdminPartInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BuildValidationProblem();
        }

        try
        {
            var result = await _catalogService.CreatePartAsync(new AdminPartEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            return Json(new AdminEntityRowViewModel { Id = result.Id, Name = result.Name, Code = result.Code });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpPut("api/parts/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePartApi(Guid id, [FromBody] AdminPartInputModel input, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Некорректный идентификатор.");
        }

        if (!ModelState.IsValid)
        {
            return BuildValidationProblem();
        }

        try
        {
            var result = await _catalogService.UpdatePartAsync(id, new AdminPartEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            return Json(new AdminEntityRowViewModel { Id = result.Id, Name = result.Name, Code = result.Code });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpDelete("api/parts/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePartApi(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Некорректный идентификатор.");
            return BuildValidationProblem();
        }

        try
        {
            await _catalogService.DeletePartAsync(id, cancellationToken).ConfigureAwait(false);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpPost("api/operations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOperationApi([FromBody] AdminOperationInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BuildValidationProblem();
        }

        try
        {
            var result = await _catalogService.CreateOperationAsync(new AdminOperationEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            return Json(new AdminEntityRowViewModel { Id = result.Id, Name = result.Name, Code = result.Code });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpPut("api/operations/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOperationApi(Guid id, [FromBody] AdminOperationInputModel input, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Некорректный идентификатор.");
        }

        if (!ModelState.IsValid)
        {
            return BuildValidationProblem();
        }

        try
        {
            var result = await _catalogService.UpdateOperationAsync(id, new AdminOperationEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            return Json(new AdminEntityRowViewModel { Id = result.Id, Name = result.Name, Code = result.Code });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpDelete("api/operations/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOperationApi(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Некорректный идентификатор.");
            return BuildValidationProblem();
        }

        try
        {
            await _catalogService.DeleteOperationAsync(id, cancellationToken).ConfigureAwait(false);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpPost("api/sections")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSectionApi([FromBody] AdminSectionInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BuildValidationProblem();
        }

        try
        {
            var result = await _catalogService.CreateSectionAsync(new AdminSectionEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            return Json(new AdminEntityRowViewModel { Id = result.Id, Name = result.Name, Code = result.Code });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpPut("api/sections/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSectionApi(Guid id, [FromBody] AdminSectionInputModel input, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Некорректный идентификатор.");
        }

        if (!ModelState.IsValid)
        {
            return BuildValidationProblem();
        }

        try
        {
            var result = await _catalogService.UpdateSectionAsync(id, new AdminSectionEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            return Json(new AdminEntityRowViewModel { Id = result.Id, Name = result.Name, Code = result.Code });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpDelete("api/sections/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSectionApi(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Некорректный идентификатор.");
            return BuildValidationProblem();
        }

        try
        {
            await _catalogService.DeleteSectionAsync(id, cancellationToken).ConfigureAwait(false);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpPost("api/balances")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWipBalanceApi([FromBody] AdminWipBalanceInputModel input, CancellationToken cancellationToken)
    {
        ValidateBalance(input);
        if (!ModelState.IsValid)
        {
            return BuildValidationProblem();
        }

        try
        {
            var result = await _catalogService.CreateWipBalanceAsync(
                new AdminWipBalanceEditDto(
                    input.PartId,
                    input.SectionId,
                    ParseOperationNumber(input.OpNumber, nameof(AdminWipBalanceInputModel.OpNumber)),
                    input.Quantity,
                    input.OperationId,
                    input.OperationLabel),
                cancellationToken).ConfigureAwait(false);
            return Json(MapBalance(result));
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpPut("api/balances/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWipBalanceApi(Guid id, [FromBody] AdminWipBalanceInputModel input, CancellationToken cancellationToken)
    {
        ValidateBalance(input);
        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Некорректный идентификатор.");
        }

        if (!ModelState.IsValid)
        {
            return BuildValidationProblem();
        }

        try
        {
            var result = await _catalogService.UpdateWipBalanceAsync(
                id,
                new AdminWipBalanceEditDto(
                    input.PartId,
                    input.SectionId,
                    ParseOperationNumber(input.OpNumber, nameof(AdminWipBalanceInputModel.OpNumber)),
                    input.Quantity,
                    input.OperationId,
                    input.OperationLabel),
                cancellationToken).ConfigureAwait(false);
            return Json(MapBalance(result));
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
    }

    [HttpDelete("api/balances/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWipBalanceApi(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Некорректный идентификатор.");
            return BuildValidationProblem();
        }

        try
        {
            await _catalogService.DeleteWipBalanceAsync(id, cancellationToken).ConfigureAwait(false);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(ex.Message);
        }
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
            OperationId = x.OperationId,
            OperationName = x.OperationName,
            OperationLabel = x.OperationLabel,
            OpNumber = x.OpNumber,
            OpNumberFormatted = OperationNumber.Format(x.OpNumber),
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
                    || ContainsText(x.OperationName, search)
                    || (!string.IsNullOrEmpty(x.OperationLabel) && ContainsText(x.OperationLabel!, search))
                    || x.OpNumberFormatted.Contains(search, StringComparison.CurrentCultureIgnoreCase)
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

        var operationOptions = operationRows
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
            OperationOptions = operationOptions,
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

    private IActionResult BuildValidationProblem()
    {
        var messages = ModelState.Values
            .SelectMany(x => x.Errors)
            .Select(x => x.ErrorMessage)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return BadRequest(new { errors = messages });
    }

    private IActionResult BuildErrorResponse(string message)
    {
        return BadRequest(new { errors = new[] { message } });
    }

    private void ValidateBalance(AdminWipBalanceInputModel input)
    {
        if (input.PartId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(AdminWipBalanceInputModel.PartId), "Выберите деталь.");
        }

        if (input.SectionId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(AdminWipBalanceInputModel.SectionId), "Выберите участок.");
        }

        if (!OperationNumber.TryParse(input.OpNumber, out _))
        {
            ModelState.AddModelError(nameof(AdminWipBalanceInputModel.OpNumber), "Номер операции должен содержать от 1 до 10 цифр и может включать дробную часть через «/».");
        }
    }

    private static AdminCatalogWipBalanceRowViewModel MapBalance(AdminWipBalanceDto dto)
    {
        return new AdminCatalogWipBalanceRowViewModel
        {
            Id = dto.Id,
            PartId = dto.PartId,
            PartName = dto.PartName,
            SectionId = dto.SectionId,
            SectionName = dto.SectionName,
            OperationId = dto.OperationId,
            OperationName = dto.OperationName,
            OperationLabel = dto.OperationLabel,
            OpNumber = dto.OpNumber,
            OpNumberFormatted = OperationNumber.Format(dto.OpNumber),
            Quantity = dto.Quantity,
        };
    }

    private static IEnumerable<AdminEntityRowViewModel> SortEntities(
        IEnumerable<AdminEntityRowViewModel> source,
        string? sort,
        string? order)
    {
        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        return sort switch
        {
            "code" => descending
                ? source.OrderByDescending(x => x.Code)
                : source.OrderBy(x => x.Code),
            _ => descending
                ? source.OrderByDescending(x => x.Name)
                : source.OrderBy(x => x.Name),
        };
    }

    private static IEnumerable<AdminCatalogWipBalanceRowViewModel> SortBalances(
        IEnumerable<AdminCatalogWipBalanceRowViewModel> source,
        string? sort,
        string? order)
    {
        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);

        return sort switch
        {
            "operationName" => descending
                ? source.OrderByDescending(x => x.OperationName)
                : source.OrderBy(x => x.OperationName),
            "operationLabel" => descending
                ? source.OrderByDescending(x => x.OperationLabel)
                : source.OrderBy(x => x.OperationLabel),
            "sectionName" => descending
                ? source.OrderByDescending(x => x.SectionName)
                : source.OrderBy(x => x.SectionName),
            "opNumber" => descending
                ? source.OrderByDescending(x => x.OpNumber)
                : source.OrderBy(x => x.OpNumber),
            "quantity" => descending
                ? source.OrderByDescending(x => x.Quantity)
                : source.OrderBy(x => x.Quantity),
            _ => descending
                ? source.OrderByDescending(x => x.PartName)
                : source.OrderBy(x => x.PartName),
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

    private static int ParseOperationNumber(string value, string parameterName)
    {
        return OperationNumber.Parse(OperationNumber.Normalize(value), parameterName);
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
