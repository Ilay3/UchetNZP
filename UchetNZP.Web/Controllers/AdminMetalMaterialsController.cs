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
        var normalizedProfile = NormalizeProfile(input.ProfileType);
        if (normalizedProfile is null)
        {
            ModelState.AddModelError(nameof(input.ProfileType), "Укажите корректный тип профиля.");
        }

        if (!input.MassPerUnitKg.HasValue || input.MassPerUnitKg.Value <= 0m)
        {
            ModelState.AddModelError(nameof(input.MassPerUnitKg), "Укажите массу на единицу больше 0.");
        }

        var normalizedCode = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim();
        if (normalizedCode is not null)
        {
            var duplicateCodeExists = await _dbContext.MetalMaterials
                .AnyAsync(x => x.Code == normalizedCode, cancellationToken);
            if (duplicateCodeExists)
            {
                ModelState.AddModelError(nameof(input.Code), "Материал с таким кодом уже существует.");
            }
        }

        if (!ModelState.IsValid || normalizedProfile is null)
        {
            var invalidModel = await BuildPageModelAsync(input, cancellationToken);
            return View("~/Views/AdminMetalMaterials/Index.cshtml", invalidModel);
        }

        var unitKind = normalizedProfile == "sheet" ? "SquareMeter" : "Meter";
        var stockUnit = normalizedProfile == "sheet" ? "m2" : "m";
        var weight = input.MassPerUnitKg!.Value;
        var material = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim(),
            Code = normalizedCode,
            DisplayName = input.Name.Trim(),
            UnitKind = unitKind,
            StockUnit = stockUnit,
            MassPerMeterKg = normalizedProfile == "sheet" ? 0m : weight,
            MassPerSquareMeterKg = normalizedProfile == "sheet" ? weight : 0m,
            CoefConsumption = 1m,
            Coefficient = 1m,
            WeightPerUnitKg = weight,
            IsActive = input.IsActive,
        };

        _dbContext.MetalMaterials.Add(material);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminMetalMaterialsStatus"] = "Материал успешно добавлен.";
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
                UnitKind = x.UnitKind,
                ProfileType = x.UnitKind == "SquareMeter" ? "Лист" : "Круг/пруток/труба",
                MassPerUnitKg = x.UnitKind == "SquareMeter" ? x.MassPerSquareMeterKg : x.MassPerMeterKg,
                IsActive = x.IsActive,
            })
            .ToListAsync(cancellationToken);

        return new AdminMetalMaterialsPageViewModel
        {
            CreateModel = input,
            Materials = materials,
        };
    }

    private static string? NormalizeProfile(string? profileType)
    {
        var value = (profileType ?? string.Empty).Trim().ToLowerInvariant();
        return value is "sheet" or "rod" or "pipe" ? value : null;
    }
}
