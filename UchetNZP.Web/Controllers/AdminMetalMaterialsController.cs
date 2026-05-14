using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Web.Infrastructure;

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

    [HttpGet("parts")]
    public async Task<IActionResult> GetParts([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Parts.AsNoTracking();
        query = query.WhereMatchesLookup(search, x => x.Name, x => x.Code);

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("materials")]
    public async Task<IActionResult> GetMaterials([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.MetalMaterials.AsNoTracking().Where(x => x.IsActive);
        query = query.WhereMatchesLookup(search, x => x.Name, x => x.Code, x => x.Article);

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken);

        return Ok(items);
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
        var unitKind = NormalizeUnitKind(input.UnitKind);
        var stockUnit = NormalizeOptional(input.StockUnit) ?? GetDefaultStockUnit(unitKind);
        var massPerMeterKg = input.MassPerMeterKg.GetValueOrDefault();
        var massPerSquareMeterKg = input.MassPerSquareMeterKg.GetValueOrDefault();
        if (unitKind == "Meter" && massPerMeterKg <= 0m)
        {
            massPerMeterKg = weight;
        }
        if (unitKind == "SquareMeter" && massPerSquareMeterKg <= 0m)
        {
            massPerSquareMeterKg = weight;
        }

        var material = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim(),
            Code = normalizedCode,
            FullName = string.IsNullOrWhiteSpace(input.FullName) ? input.Name.Trim() : input.FullName.Trim(),
            Article = NormalizeOptional(input.Article),
            UnitOfMeasure = string.IsNullOrWhiteSpace(input.UnitOfMeasure) ? "кг" : input.UnitOfMeasure.Trim(),
            NomenclatureType = NormalizeOptional(input.NomenclatureType) ?? "Материалы",
            NomenclatureGroup = NormalizeOptional(input.NomenclatureGroup) ?? "Металл",
            VatRateType = NormalizeOptional(input.VatRateType) ?? "НДС 22%",
            CountryOfOrigin = NormalizeOptional(input.CountryOfOrigin),
            CustomsDeclarationNumber = NormalizeOptional(input.CustomsDeclarationNumber),
            Okpd2Code = NormalizeOptional(input.Okpd2Code),
            TnVedCode = NormalizeOptional(input.TnVedCode),
            Comment = NormalizeOptional(input.Comment),
            IsService = input.IsService,
            DisplayName = input.Name.Trim(),
            UnitKind = unitKind,
            StockUnit = stockUnit,
            MassPerMeterKg = massPerMeterKg,
            MassPerSquareMeterKg = massPerSquareMeterKg,
            CoefConsumption = input.CoefConsumption!.Value,
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
        var unitKind = NormalizeUnitKind(input.UnitKind);
        var stockUnit = NormalizeOptional(input.StockUnit) ?? GetDefaultStockUnit(unitKind);
        var massPerMeterKg = input.MassPerMeterKg.GetValueOrDefault();
        var massPerSquareMeterKg = input.MassPerSquareMeterKg.GetValueOrDefault();
        if (unitKind == "Meter" && massPerMeterKg <= 0m)
        {
            massPerMeterKg = weight;
        }
        if (unitKind == "SquareMeter" && massPerSquareMeterKg <= 0m)
        {
            massPerSquareMeterKg = weight;
        }

        material.Name = input.Name.Trim();
        material.Code = normalizedCode;
        material.FullName = string.IsNullOrWhiteSpace(input.FullName) ? input.Name.Trim() : input.FullName.Trim();
        material.Article = NormalizeOptional(input.Article);
        material.UnitOfMeasure = string.IsNullOrWhiteSpace(input.UnitOfMeasure) ? "кг" : input.UnitOfMeasure.Trim();
        material.NomenclatureType = NormalizeOptional(input.NomenclatureType) ?? "Материалы";
        material.NomenclatureGroup = NormalizeOptional(input.NomenclatureGroup) ?? "Металл";
        material.VatRateType = NormalizeOptional(input.VatRateType) ?? "НДС 22%";
        material.CountryOfOrigin = NormalizeOptional(input.CountryOfOrigin);
        material.CustomsDeclarationNumber = NormalizeOptional(input.CustomsDeclarationNumber);
        material.Okpd2Code = NormalizeOptional(input.Okpd2Code);
        material.TnVedCode = NormalizeOptional(input.TnVedCode);
        material.Comment = NormalizeOptional(input.Comment);
        material.IsService = input.IsService;
        material.DisplayName = input.Name.Trim();
        material.UnitKind = unitKind;
        material.StockUnit = stockUnit;
        material.MassPerMeterKg = massPerMeterKg;
        material.MassPerSquareMeterKg = massPerSquareMeterKg;
        material.CoefConsumption = input.CoefConsumption!.Value;
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

    [HttpPost("part-norms/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePartNorm(AdminPartMaterialNormCreateInputModel input, CancellationToken cancellationToken)
    {
        var referenceNorm = await _dbContext.MetalConsumptionNorms
            .AsNoTracking()
            .Where(x => x.PartId == input.PartId && x.IsActive && x.BaseConsumptionQty > 0m)
            .OrderBy(x => x.MetalMaterialId.HasValue)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (referenceNorm is null)
        {
            TempData["AdminMetalMaterialsError"] = "У детали нет активной нормы расхода. Сначала задайте норму в карточке детали.";
            return RedirectToAction(nameof(Index));
        }

        var existingCount = await _dbContext.MetalConsumptionNorms.CountAsync(x => x.PartId == input.PartId && x.IsActive, cancellationToken);
        if (existingCount >= 8)
        {
            TempData["AdminMetalMaterialsError"] = "Для одной детали можно указать не более 8 материалов.";
            return RedirectToAction(nameof(Index));
        }

        var duplicate = await _dbContext.MetalConsumptionNorms.FirstOrDefaultAsync(x => x.PartId == input.PartId && x.MetalMaterialId == input.MetalMaterialId && x.IsActive, cancellationToken);
        if (duplicate is not null)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["AdminMetalMaterialsStatus"] = "Связь деталь-материал уже существует.";
            return RedirectToAction(nameof(Index));
        }

        _dbContext.MetalConsumptionNorms.Add(new MetalConsumptionNorm
        {
            Id = Guid.NewGuid(),
            PartId = input.PartId,
            MetalMaterialId = input.MetalMaterialId,
            BaseConsumptionQty = referenceNorm.BaseConsumptionQty,
            ConsumptionUnit = referenceNorm.ConsumptionUnit,
            NormalizedConsumptionUnit = referenceNorm.NormalizedConsumptionUnit,
            UnitNorm = referenceNorm.UnitNorm,
            SizeRaw = referenceNorm.SizeRaw,
            NormalizedSizeRaw = referenceNorm.NormalizedSizeRaw,
            NormKeyHash = $"{input.PartId:N}:{input.MetalMaterialId:N}",
            ShapeType = referenceNorm.ShapeType,
            ParseStatus = "manual",
            IsActive = true,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminMetalMaterialsStatus"] = "Связь деталь-материал добавлена.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("part-norms/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePartNorm(Guid id, CancellationToken cancellationToken)
    {
        var norm = await _dbContext.MetalConsumptionNorms.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (norm is null)
        {
            TempData["AdminMetalMaterialsError"] = "Связь не найдена.";
            return RedirectToAction(nameof(Index));
        }
        norm.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminMetalMaterialsStatus"] = "Связь деталь-материал деактивирована.";
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
                FullName = x.FullName,
                Code = x.Code,
                Article = x.Article,
                UnitOfMeasure = x.UnitOfMeasure,
                NomenclatureType = x.NomenclatureType,
                NomenclatureGroup = x.NomenclatureGroup,
                VatRateType = x.VatRateType,
                CountryOfOrigin = x.CountryOfOrigin,
                CustomsDeclarationNumber = x.CustomsDeclarationNumber,
                TnVedCode = x.TnVedCode,
                Okpd2Code = x.Okpd2Code,
                Comment = x.Comment,
                IsService = x.IsService,
                UnitKind = x.UnitKind,
                StockUnit = x.StockUnit,
                MassPerMeterKg = x.MassPerMeterKg,
                MassPerSquareMeterKg = x.MassPerSquareMeterKg,
                CoefConsumption = x.CoefConsumption,
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
            Parts = await _dbContext.Parts.AsNoTracking().OrderBy(x => x.Name).Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code)).Take(500).ToListAsync(cancellationToken),
            ActiveMaterials = await _dbContext.MetalMaterials.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken),
            PartMaterialNorms = await _dbContext.MetalConsumptionNorms
                .AsNoTracking()
                .Where(x => x.IsActive && x.MetalMaterialId.HasValue)
                .Include(x => x.Part)
                .Include(x => x.MetalMaterial)
                .OrderBy(x => x.Part!.Name)
                .ThenBy(x => x.MetalMaterial!.Name)
                .Select(x => new AdminPartMaterialNormListItemViewModel
                {
                    Id = x.Id,
                    PartId = x.PartId,
                    PartName = x.Part != null ? x.Part.Name : string.Empty,
                    PartCode = x.Part != null ? x.Part.Code : null,
                    MaterialId = x.MetalMaterialId!.Value,
                    MaterialName = x.MetalMaterial != null ? x.MetalMaterial.Name : string.Empty,
                    MaterialCode = x.MetalMaterial != null ? x.MetalMaterial.Code : null,
                    BaseConsumptionQty = x.BaseConsumptionQty,
                }).ToListAsync(cancellationToken),
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

        if (!input.CoefConsumption.HasValue || input.CoefConsumption.Value <= 0m)
        {
            ModelState.AddModelError(nameof(input.CoefConsumption), "Укажите коэффициент расхода больше 0.");
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeUnitKind(string? value)
    {
        return value switch
        {
            "SquareMeter" => "SquareMeter",
            "Kilogram" => "Kilogram",
            "Piece" => "Piece",
            _ => "Meter",
        };
    }

    private static string GetDefaultStockUnit(string unitKind)
    {
        return unitKind switch
        {
            "SquareMeter" => "m2",
            "Kilogram" => "kg",
            "Piece" => "pcs",
            _ => "m",
        };
    }

    private static bool TryParseNorm(string? rawValue, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim().Replace(" ", string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out result)
            || decimal.TryParse(normalized.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(normalized.Replace('.', ','), NumberStyles.Number, new CultureInfo("ru-RU"), out result);
    }
}
