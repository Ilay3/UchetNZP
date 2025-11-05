using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var statusMessage = ReadTempData(StatusTempDataKey);
        var errorMessage = ReadTempData(ErrorTempDataKey);

        var viewModel = await BuildIndexViewModelAsync(
            cancellationToken,
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
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                partInput: input,
                errorMessage: "Исправьте ошибки формы создания детали.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.CreatePartAsync(new AdminPartEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Деталь успешно создана.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                errorMessage: "Исправьте данные для редактирования детали.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.UpdatePartAsync(input.Id, new AdminPartEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Изменения детали сохранены.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("operations/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOperation(AdminOperationInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                operationInput: input,
                errorMessage: "Исправьте ошибки формы создания операции.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.CreateOperationAsync(new AdminOperationEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Операция успешно создана.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                errorMessage: "Исправьте данные для редактирования операции.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.UpdateOperationAsync(input.Id, new AdminOperationEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Изменения операции сохранены.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("sections/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSection(AdminSectionInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                sectionInput: input,
                errorMessage: "Исправьте ошибки формы создания участка.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.CreateSectionAsync(new AdminSectionEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Участок успешно создан.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
                errorMessage: "Исправьте данные для редактирования участка.").ConfigureAwait(false);

            return View("Index", viewModel);
        }

        try
        {
            await _catalogService.UpdateSectionAsync(input.Id, new AdminSectionEditDto(input.Name, input.Code), cancellationToken).ConfigureAwait(false);
            TempData[StatusTempDataKey] = "Изменения участка сохранены.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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

        return RedirectToAction(nameof(Index));
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
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var viewModel = await BuildIndexViewModelAsync(
                cancellationToken,
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

        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminIndexViewModel> BuildIndexViewModelAsync(
        CancellationToken cancellationToken,
        AdminPartInputModel? partInput = null,
        AdminOperationInputModel? operationInput = null,
        AdminSectionInputModel? sectionInput = null,
        AdminWipBalanceInputModel? wipBalanceInput = null,
        string? statusMessage = null,
        string? errorMessage = null)
    {
        var partDtos = await _catalogService.GetPartsAsync(cancellationToken).ConfigureAwait(false);
        var operationDtos = await _catalogService.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        var sectionDtos = await _catalogService.GetSectionsAsync(cancellationToken).ConfigureAwait(false);
        var balanceDtos = await _catalogService.GetWipBalancesAsync(cancellationToken).ConfigureAwait(false);

        var parts = partDtos
            .Select(x => new AdminEntityRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
            })
            .ToList();

        var operations = operationDtos
            .Select(x => new AdminEntityRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
            })
            .ToList();

        var sections = sectionDtos
            .Select(x => new AdminEntityRowViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
            })
            .ToList();

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

        var partOptions = parts
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = FormatEntityDisplay(x.Name, x.Code),
            })
            .ToList();

        var sectionOptions = sections
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
}
