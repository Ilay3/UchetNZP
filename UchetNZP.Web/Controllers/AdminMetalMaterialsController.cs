using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Authorize]
[Route("admin/metal-materials")]
public class AdminMetalMaterialsController : Controller
{
    private readonly AppDbContext _dbContext;

    public AdminMetalMaterialsController(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildPageModelAsync(new AdminMetalMaterialCreateInputModel(), cancellationToken);
        return View("~/Views/AdminMetalMaterials/Index.cshtml", model);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminMetalMaterialCreateInputModel input, CancellationToken cancellationToken)
    {
        var normalizedCode = await ValidateInputAsync(input, null, cancellationToken);

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildPageModelAsync(input, cancellationToken);
            return View("~/Views/AdminMetalMaterials/Index.cshtml", invalidModel);
        }

        var weight = input.WeightPerUnitKg!.Value;
        var coefficient = input.Coefficient!.Value;
        var material = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim(),
            Code = normalizedCode,
            DisplayName = input.Name.Trim(),
            UnitKind = "Meter",
            StockUnit = "m",
            MassPerMeterKg = weight,
            MassPerSquareMeterKg = weight,
            CoefConsumption = 1m,
            Coefficient = coefficient,
            WeightPerUnitKg = weight,
            IsActive = input.IsActive,
        };

        _dbContext.MetalMaterials.Add(material);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminMetalMaterialsStatus"] = "Материал успешно добавлен.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(AdminMetalMaterialUpdateInputModel input, CancellationToken cancellationToken)
    {
        var material = await _dbContext.MetalMaterials.FirstOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
        if (material is null)
        {
            TempData["AdminMetalMaterialsError"] = "Материал не найден.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedCode = await ValidateInputAsync(input, input.Id, cancellationToken);
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildPageModelAsync(input, cancellationToken);
            return View("~/Views/AdminMetalMaterials/Index.cshtml", invalidModel);
        }

        var weight = input.WeightPerUnitKg!.Value;
        var coefficient = input.Coefficient!.Value;
        material.Name = input.Name.Trim();
        material.Code = normalizedCode;
        material.DisplayName = input.Name.Trim();
        material.UnitKind = "Meter";
        material.StockUnit = "m";
        material.MassPerMeterKg = weight;
        material.MassPerSquareMeterKg = weight;
        material.WeightPerUnitKg = weight;
        material.Coefficient = coefficient;
        material.IsActive = input.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminMetalMaterialsStatus"] = "Материал успешно обновлён.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var material = await _dbContext.MetalMaterials.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (material is null)
        {
            TempData["AdminMetalMaterialsError"] = "Материал не найден.";
            return RedirectToAction(nameof(Index));
        }

        var hasReceiptItems = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .AnyAsync(x => x.MetalMaterialId == id, cancellationToken);

        if (hasReceiptItems)
        {
            material.IsActive = false;
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["AdminMetalMaterialsStatus"] = "Материал используется в приходах и был деактивирован.";
            return RedirectToAction(nameof(Index));
        }

        _dbContext.MetalMaterials.Remove(material);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminMetalMaterialsStatus"] = "Материал удалён.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminMetalMaterialsPageViewModel> BuildPageModelAsync(AdminMetalMaterialCreateInputModel input, CancellationToken cancellationToken)
    {
        var materials = await _dbContext.MetalMaterials
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdminMetalMaterialListItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                WeightPerUnitKg = (x.WeightPerUnitKg ?? 0m) > 0m ? x.WeightPerUnitKg!.Value : (x.MassPerMeterKg > 0m ? x.MassPerMeterKg : x.MassPerSquareMeterKg),
                Coefficient = x.Coefficient,
                IsActive = x.IsActive,
            })
            .ToListAsync(cancellationToken);

        return new AdminMetalMaterialsPageViewModel
        {
            CreateModel = input,
            UpdateModel = new AdminMetalMaterialUpdateInputModel(),
            Materials = materials,
        };
    }

    private async Task<string?> ValidateInputAsync(AdminMetalMaterialCreateInputModel input, Guid? currentId, CancellationToken cancellationToken)
    {
        if (!input.WeightPerUnitKg.HasValue || input.WeightPerUnitKg.Value <= 0m)
        {
            ModelState.AddModelError(nameof(input.WeightPerUnitKg), "Укажите вес 1м / 1м² больше 0.");
        }

        if (!input.Coefficient.HasValue || input.Coefficient.Value <= 0m)
        {
            ModelState.AddModelError(nameof(input.Coefficient), "Укажите коэффициент металла больше 0.");
        }

        var normalizedCode = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim();
        if (normalizedCode is not null)
        {
            var duplicateCodeExists = await _dbContext.MetalMaterials
                .AnyAsync(x => x.Code == normalizedCode && (!currentId.HasValue || x.Id != currentId.Value), cancellationToken);
            if (duplicateCodeExists)
            {
                ModelState.AddModelError(nameof(input.Code), "Материал с таким кодом уже существует.");
            }
        }

        return normalizedCode;
    }
}
