using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
using System.Text.Json;

namespace UchetNZP.Web.Controllers;

[Route("MetalWarehouse")]
public class MetalWarehouseController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ICuttingMapExcelExporter _cuttingMapExcelExporter;
    private readonly ICuttingMapPdfExporter _cuttingMapPdfExporter;

    public MetalWarehouseController(
        AppDbContext dbContext,
        ICuttingMapExcelExporter cuttingMapExcelExporter,
        ICuttingMapPdfExporter cuttingMapPdfExporter)
    {
        _dbContext = dbContext;
        _cuttingMapExcelExporter = cuttingMapExcelExporter;
        _cuttingMapPdfExporter = cuttingMapPdfExporter;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new MetalWarehouseDashboardViewModel
        {
            MaterialsInCatalog = await _dbContext.Parts.AsNoTracking().CountAsync(cancellationToken),
            MetalUnitsInStock = await _dbContext.WarehouseItems.AsNoTracking().SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m,
            OpenRequirements = await _dbContext.MetalRequirements.AsNoTracking().CountAsync(x => x.Status == "Создано", cancellationToken),
            MovementsToday = 0,
        };

        return View(model);
    }

    [HttpGet("Receipts")]
    public async Task<IActionResult> Receipts(CancellationToken cancellationToken)
    {
        var receipts = await _dbContext.MetalReceipts
            .AsNoTracking()
            .OrderByDescending(x => x.ReceiptDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new MetalReceiptListItemViewModel
            {
                Id = x.Id,
                ReceiptNumber = x.ReceiptNumber,
                ReceiptDate = x.ReceiptDate,
                SupplierOrSource = x.SupplierOrSource ?? string.Empty,
                PositionsCount = x.Items.Count,
            })
            .ToListAsync(cancellationToken);

        var model = new MetalReceiptListViewModel
        {
            Receipts = receipts,
        };

        return View(model);
    }

    [HttpGet("Receipts/Create")]
    public async Task<IActionResult> CreateReceipt(CancellationToken cancellationToken)
    {
        await EnsureMetalMaterialsSeededAsync(cancellationToken);
        var model = new MetalReceiptCreateViewModel
        {
            ReceiptDate = DateTime.Today,
        };

        await PopulateMaterialsAsync(model, cancellationToken);
        return View("~/Views/MetalWarehouse/CreateReceipt.cshtml", model);
    }

    [HttpPost("Receipts/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReceipt(MetalReceiptCreateViewModel model, CancellationToken cancellationToken)
    {
        await EnsureMetalMaterialsSeededAsync(cancellationToken);

        if (model.Quantity.HasValue && model.Quantity.Value > 0 && model.Units.Count != model.Quantity.Value)
        {
            model.Units = Enumerable.Range(1, model.Quantity.Value)
                .Select(i => new MetalReceiptUnitInputViewModel
                {
                    ItemIndex = i,
                    SizeValue = model.Units.FirstOrDefault(x => x.ItemIndex == i)?.SizeValue,
                })
                .ToList();
        }

        var hasActiveMaterials = await _dbContext.MetalMaterials
            .AsNoTracking()
            .AnyAsync(x => x.IsActive, cancellationToken);

        if (!hasActiveMaterials)
        {
            ModelState.AddModelError(string.Empty, "Справочник материалов пуст. Обратитесь к администратору.");
        }

        var material = model.MetalMaterialId.HasValue
            ? await _dbContext.MetalMaterials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.MetalMaterialId.Value && x.IsActive, cancellationToken)
            : null;

        if (model.MetalMaterialId.HasValue && material is null)
        {
            ModelState.AddModelError(nameof(model.MetalMaterialId), "Выбранный материал не найден.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateMaterialsAsync(model, cancellationToken);
            return View("~/Views/MetalWarehouse/CreateReceipt.cshtml", model);
        }

        ArgumentNullException.ThrowIfNull(material);

        var now = DateTime.UtcNow;
        var nextNumber = await GetNextReceiptNumberAsync(cancellationToken);
        var receipt = new MetalReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = nextNumber,
            ReceiptDate = ToUtcDate(model.ReceiptDate!.Value),
            SupplierOrSource = model.SupplierOrSource?.Trim(),
            Comment = model.Comment?.Trim(),
            CreatedAt = now,
        };

        var profileType = ResolveProfileType(material.Name, model.ProfileType);
        var materialCode = BuildMaterialCode(material);
        var quantity = model.Quantity!.Value;
        var passportWeight = model.PassportWeightKg!.Value;
        var actualWeight = model.ActualWeightKg!.Value;
        var calculatedWeight = CalculateWeightKg(profileType, model, quantity, material);
        var batchNumber = string.IsNullOrWhiteSpace(model.BatchNumber) ? nextNumber : model.BatchNumber.Trim();
        var deviation = actualWeight - passportWeight;
        if (Math.Abs(deviation) > Math.Max(0.5m, passportWeight * 0.02m))
        {
            ModelState.AddModelError(nameof(model.ActualWeightKg), $"Расхождение между паспортной и фактической массой превышает допуск: {deviation:0.###} кг.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateMaterialsAsync(model, cancellationToken);
            return View("~/Views/MetalWarehouse/CreateReceipt.cshtml", model);
        }

        receipt.BatchNumber = batchNumber;
        for (var i = 0; i < quantity; i++)
        {
            var unit = model.Units[i];
            var suffix = (i + 1).ToString("D3");
            var (sizeValue, sizeUnitText) = ResolveSizeFromInputOrMass(profileType, unit.SizeValue, model, material, quantity, actualWeight);
            var sizePart = sizeValue.ToString("0.###").Replace('.', '_');
            receipt.Items.Add(new MetalReceiptItem
            {
                Id = Guid.NewGuid(),
                MetalMaterialId = material.Id,
                Quantity = quantity,
                TotalWeightKg = actualWeight,
                ItemIndex = i + 1,
                SizeValue = sizeValue,
                SizeUnitText = sizeUnitText,
                ProfileType = profileType,
                ThicknessMm = model.ThicknessMm,
                WidthMm = model.WidthMm,
                LengthMm = model.LengthMm,
                DiameterMm = model.DiameterMm,
                WallThicknessMm = model.WallThicknessMm,
                PassportWeightKg = passportWeight,
                ActualWeightKg = actualWeight,
                CalculatedWeightKg = calculatedWeight,
                WeightDeviationKg = deviation,
                StockCategory = ResolveStockCategory(profileType, sizeValue),
                GeneratedCode = $"{materialCode}-{sizePart}-{(sizeUnitText == "м2" ? "M2" : "M")}-{suffix}",
                CreatedAt = now,
            });
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.MetalReceipts.Add(receipt);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["MetalReceiptSuccess"] = "Приход металла успешно создан";

        return RedirectToAction(nameof(ReceiptDetails), new { id = receipt.Id });
    }

    [HttpGet("Receipts/Details/{id:guid}")]
    public async Task<IActionResult> ReceiptDetails(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await _dbContext.MetalReceipts
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.ReceiptNumber,
                x.ReceiptDate,
                x.SupplierOrSource,
                x.BatchNumber,
                x.Comment,
                Item = x.Items
                    .OrderBy(i => i.ItemIndex)
                    .Select(i => new
                    {
                        i.ItemIndex,
                        i.SizeValue,
                        i.SizeUnitText,
                        i.GeneratedCode,
                        i.PassportWeightKg,
                        i.ActualWeightKg,
                        i.CalculatedWeightKg,
                        i.WeightDeviationKg,
                        i.ProfileType,
                        i.Quantity,
                        MaterialName = i.MetalMaterial != null ? i.MetalMaterial.Name : string.Empty,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (receipt is null || receipt.Item.Count == 0)
        {
            return NotFound();
        }

        var first = receipt.Item[0];
        var model = new MetalReceiptDetailsViewModel
        {
            Id = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate,
            SupplierOrSource = receipt.SupplierOrSource ?? string.Empty,
            Comment = receipt.Comment,
            MaterialName = first.MaterialName,
            PassportWeightKg = first.PassportWeightKg,
            ActualWeightKg = first.ActualWeightKg,
            CalculatedWeightKg = first.CalculatedWeightKg,
            WeightDeviationKg = first.WeightDeviationKg,
            BatchNumber = receipt.BatchNumber,
            ProfileTypeDisplay = ToProfileCaption(first.ProfileType),
            Quantity = (int)first.Quantity,
            Items = receipt.Item
                .Select(i => new MetalReceiptDetailsItemViewModel
                {
                    ItemIndex = i.ItemIndex,
                    SizeValue = i.SizeValue,
                    SizeUnitText = i.SizeUnitText,
                    GeneratedCode = i.GeneratedCode,
                })
                .ToList(),
        };

        return View("~/Views/MetalWarehouse/ReceiptDetails.cshtml", model);
    }

    [HttpGet("Stock")]
    public async Task<IActionResult> Stock([FromQuery] MetalStockFilterViewModel filter, CancellationToken cancellationToken)
    {
        var stockQuery = _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.MetalMaterialId,
                MaterialName = x.MetalMaterial != null ? x.MetalMaterial.Name : "-",
                x.GeneratedCode,
                x.SizeValue,
                x.SizeUnitText,
                x.StockCategory,
                WeightKg = x.Quantity > 0 ? x.TotalWeightKg / x.Quantity : x.TotalWeightKg,
                ReceiptNumber = x.MetalReceipt != null ? x.MetalReceipt.ReceiptNumber : string.Empty,
                ReceiptDate = x.MetalReceipt != null ? x.MetalReceipt.ReceiptDate : x.CreatedAt,
                BatchNumber = x.MetalReceipt != null ? x.MetalReceipt.BatchNumber : string.Empty,
            });

        if (filter.MaterialId.HasValue)
        {
            stockQuery = stockQuery.Where(x => x.MetalMaterialId == filter.MaterialId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.UnitCodeOrNumber))
        {
            var codeFilter = filter.UnitCodeOrNumber.Trim();
            stockQuery = stockQuery.Where(x => x.GeneratedCode.Contains(codeFilter));
        }

        if (!string.IsNullOrWhiteSpace(filter.UnitOfMeasure))
        {
            var unitFilter = filter.UnitOfMeasure.Trim();
            stockQuery = stockQuery.Where(x => x.SizeUnitText == unitFilter);
        }

        var stockRows = await stockQuery
            .OrderBy(x => x.ReceiptDate)
            .ThenBy(x => x.GeneratedCode)
            .ToListAsync(cancellationToken);

        var materialOptions = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = x.Id.ToString(),
                Text = string.IsNullOrWhiteSpace(x.Code) ? x.Name : $"{x.Name} ({x.Code})",
                Selected = filter.MaterialId.HasValue && filter.MaterialId.Value == x.Id,
            })
            .ToListAsync(cancellationToken);

        var unitOptions = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Select(x => x.SizeUnitText)
            .Distinct()
            .OrderBy(x => x)
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = x,
                Text = x,
                Selected = filter.UnitOfMeasure == x,
            })
            .ToListAsync(cancellationToken);

        var model = new MetalStockPageViewModel
        {
            Filters = new MetalStockFilterViewModel
            {
                MaterialId = filter.MaterialId,
                UnitCodeOrNumber = filter.UnitCodeOrNumber,
                UnitOfMeasure = filter.UnitOfMeasure,
                ActiveOnly = filter.ActiveOnly,
                Materials = materialOptions,
                UnitOfMeasures = unitOptions,
            },
            Items = stockRows.Select(x => new MetalStockItemViewModel
            {
                Id = x.Id,
                GeneratedCode = x.GeneratedCode,
                MaterialName = x.MaterialName,
                SizeValue = x.SizeValue,
                SizeUnitText = x.SizeUnitText,
                WeightKg = x.WeightKg,
                ReceiptNumber = x.ReceiptNumber,
                ReceiptDate = x.ReceiptDate,
                BatchNumber = x.BatchNumber,
                StockCategory = x.StockCategory,
                Status = ToStockCategoryCaption(x.StockCategory),
            }).ToList(),
            TotalUnitsCount = stockRows.Count,
            TotalMaterialsCount = stockRows.Select(x => x.MetalMaterialId).Distinct().Count(),
            TotalWeightKg = stockRows.Sum(x => x.WeightKg),
            TotalSize = stockRows.Sum(x => x.SizeValue),
        };

        return View(model);
    }

    [HttpGet("Stock/Item/{id:guid}")]
    public async Task<IActionResult> StockItem(Guid id, CancellationToken cancellationToken)
    {
        var item = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.GeneratedCode,
                MaterialName = x.MetalMaterial != null ? x.MetalMaterial.Name : "-",
                x.SizeValue,
                x.SizeUnitText,
                WeightKg = x.Quantity > 0 ? x.TotalWeightKg / x.Quantity : x.TotalWeightKg,
                ReceiptNumber = x.MetalReceipt != null ? x.MetalReceipt.ReceiptNumber : string.Empty,
                ReceiptDate = x.MetalReceipt != null ? x.MetalReceipt.ReceiptDate : x.CreatedAt,
                Source = x.MetalReceipt != null ? x.MetalReceipt.SupplierOrSource : null,
                Comment = x.MetalReceipt != null ? x.MetalReceipt.Comment : null,
                CreatedAt = x.CreatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        var model = new MetalStockItemDetailsViewModel
        {
            Id = item.Id,
            GeneratedCode = item.GeneratedCode,
            MaterialName = item.MaterialName,
            SizeValue = item.SizeValue,
            SizeUnitText = item.SizeUnitText,
            WeightKg = item.WeightKg,
            ReceiptNumber = item.ReceiptNumber,
            ReceiptDate = item.ReceiptDate,
            Source = item.Source ?? "Не указан",
            ReceiptComment = item.Comment,
            History =
            [
                new MetalStockItemHistoryEntryViewModel
                {
                    Timestamp = item.CreatedAt,
                    EventName = "Приход",
                    Description = $"Единица добавлена по документу {item.ReceiptNumber}.",
                },
            ],
        };

        return View("~/Views/MetalWarehouse/StockItem.cshtml", model);
    }

    [HttpGet("Requirements")]
    public async Task<IActionResult> Requirements(CancellationToken cancellationToken)
    {
        var items = await _dbContext.MetalRequirements
            .AsNoTracking()
            .OrderByDescending(x => x.RequirementDate)
            .ThenByDescending(x => x.CreatedAt)
            .SelectMany(
                x => x.Items.DefaultIfEmpty(),
                (requirement, item) => new MetalRequirementListItemViewModel
                {
                    Id = requirement.Id,
                    RequirementNumber = requirement.RequirementNumber,
                    RequirementDate = requirement.RequirementDate,
                    PartDisplay = requirement.Part != null
                        ? (string.IsNullOrWhiteSpace(requirement.Part.Code) ? requirement.Part.Name : $"{requirement.Part.Name} ({requirement.Part.Code})")
                        : string.Empty,
                    Quantity = requirement.Quantity,
                    MaterialDisplay = item != null
                        ? (item.MetalMaterial != null && !string.IsNullOrWhiteSpace(item.MetalMaterial.Code)
                            ? $"{item.MetalMaterial.Name} ({item.MetalMaterial.Code})"
                            : (item.MetalMaterial != null ? item.MetalMaterial.Name : "—"))
                        : "—",
                    RequiredQty = item != null ? item.TotalRequiredQty : 0m,
                    Unit = item != null ? item.Unit : string.Empty,
                    Status = requirement.Status,
                })
            .ToListAsync(cancellationToken);

        return View(model: new MetalRequirementListViewModel
        {
            Items = items,
        });
    }

    [HttpGet("Requirements/Details/{id:guid}")]
    public async Task<IActionResult> RequirementDetails(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildRequirementDetailsModelAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View("~/Views/MetalWarehouse/RequirementDetails.cshtml", model);
    }

    [HttpGet("Requirements/Details/{id:guid}/print/warehouse")]
    public async Task<IActionResult> RequirementPrintWarehouse(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildRequirementDetailsModelAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View("~/Views/MetalWarehouse/RequirementPrintWarehouse.cshtml", model);
    }

    [HttpGet("Requirements/Details/{id:guid}/print/master")]
    public async Task<IActionResult> RequirementPrintMaster(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildRequirementDetailsModelAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View("~/Views/MetalWarehouse/RequirementPrintMaster.cshtml", model);
    }

    [HttpGet("CuttingMaps")]
    public async Task<IActionResult> CuttingMaps(CancellationToken cancellationToken)
    {
        var plans = await _dbContext.CuttingPlans
            .AsNoTracking()
            .Where(x => x.IsCurrent)
            .Include(x => x.Items)
            .Include(x => x.MetalRequirement)
                .ThenInclude(x => x!.Part)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var model = new CuttingMapListViewModel
        {
            Maps = plans.Select(MapCuttingPlan).ToList(),
        };

        return View("~/Views/MetalWarehouse/CuttingMaps.cshtml", model);
    }

    [HttpPost("CuttingMaps/{planId:guid}/execution")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCuttingMapExecution(Guid planId, string executionStatus, decimal? actualResidual, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.CuttingPlans.FirstOrDefaultAsync(x => x.Id == planId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        var normalizedStatus = (executionStatus ?? string.Empty).Trim();
        if (normalizedStatus != "выполнено" && normalizedStatus != "частично")
        {
            normalizedStatus = "частично";
        }

        plan.ExecutionStatus = normalizedStatus;
        plan.ActualResidual = actualResidual;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(CuttingMaps));
    }

    [HttpGet("CuttingMaps/{planId:guid}/export/excel")]
    public async Task<IActionResult> ExportCuttingMapExcel(Guid planId, CancellationToken cancellationToken)
    {
        var map = await BuildCuttingMapAsync(planId, cancellationToken);
        if (map is null)
        {
            return NotFound();
        }

        var file = _cuttingMapExcelExporter.Export(map);
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Карта_раскроя_{map.RequirementNumber}_v{map.Version}.xlsx");
    }

    [HttpGet("CuttingMaps/{planId:guid}/export/pdf")]
    public async Task<IActionResult> ExportCuttingMapPdf(Guid planId, CancellationToken cancellationToken)
    {
        var map = await BuildCuttingMapAsync(planId, cancellationToken);
        if (map is null)
        {
            return NotFound();
        }

        var file = _cuttingMapPdfExporter.Export(map);
        return File(file, "application/pdf", $"Карта_раскроя_{map.RequirementNumber}_v{map.Version}.pdf");
    }

    [HttpGet("Movements")]
    public IActionResult Movements()
    {
        var model = new MetalWarehouseListPageViewModel
        {
            Title = "История движений",
            Description = "Хронология всех операций по складу металла.",
            Headers = new[] { "Дата и время", "Операция", "Материал", "Количество", "Источник", "Ответственный" },
            EmptyStateTitle = "Движений пока нет",
            EmptyStateDescription = "История начнёт наполняться после проведения операций в модуле.",
        };

        return View(model);
    }

    private async Task<CuttingMapCardViewModel?> BuildCuttingMapAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.CuttingPlans
            .AsNoTracking()
            .Where(x => x.Id == planId)
            .Include(x => x.Items)
            .Include(x => x.MetalRequirement)
                .ThenInclude(x => x!.Part)
            .FirstOrDefaultAsync(cancellationToken);
        return plan is null ? null : MapCuttingPlan(plan);
    }

    private async Task<MetalRequirementDetailsViewModel?> BuildRequirementDetailsModelAsync(Guid id, CancellationToken cancellationToken)
    {
        var requirement = await _dbContext.MetalRequirements
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.RequirementNumber,
                x.RequirementDate,
                x.Status,
                PartName = x.Part != null ? x.Part.Name : string.Empty,
                PartCode = x.Part != null ? x.Part.Code : null,
                x.Quantity,
                x.WipLaunchId,
                LaunchDate = x.WipLaunch != null ? x.WipLaunch.LaunchDate : (DateTime?)null,
                x.Comment,
                Items = x.Items.Select(i => new
                {
                    i.NormPerUnit,
                    i.TotalRequiredQty,
                    i.Unit,
                    i.TotalRequiredWeightKg,
                    i.CalculationFormula,
                    i.CalculationInput,
                    MaterialName = i.MetalMaterial != null ? i.MetalMaterial.Name : "—",
                    MaterialCode = i.MetalMaterial != null ? i.MetalMaterial.Code : null,
                    i.MetalMaterialId,
                    MassPerMeterKg = i.MetalMaterial != null ? i.MetalMaterial.MassPerMeterKg : 0m,
                    MassPerSquareMeterKg = i.MetalMaterial != null ? i.MetalMaterial.MassPerSquareMeterKg : 0m,
                    CoefConsumption = i.MetalMaterial != null ? i.MetalMaterial.CoefConsumption : 1m,
                    i.SelectionSource,
                    i.SelectionReason,
                    i.CandidateMaterials,
                }).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (requirement is null)
        {
            return null;
        }

        var currentPlan = await _dbContext.CuttingPlans
            .AsNoTracking()
            .Where(x => x.MetalRequirementId == requirement.Id && x.IsCurrent)
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var materialIds = requirement.Items.Select(i => i.MetalMaterialId).Distinct().ToList();
        var stockLookup = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.MetalMaterialId))
            .GroupBy(x => x.MetalMaterialId)
            .Select(x => new
            {
                MaterialId = x.Key,
                Qty = x.Sum(i => i.SizeValue),
                WeightKg = x.Sum(i => i.Quantity > 0 ? i.TotalWeightKg / i.Quantity : i.TotalWeightKg),
            })
            .ToDictionaryAsync(x => x.MaterialId, cancellationToken);

        var sourceBlank = currentPlan is null
            ? "—"
            : (ReadDecimalFromJson(currentPlan.ParametersJson, "Linear", "StockLength") is decimal stockLength
                ? $"{stockLength:0.###} мм"
                : $"{ReadDecimalFromJson(currentPlan.ParametersJson, "Sheet", "StockWidth"):0.###} x {ReadDecimalFromJson(currentPlan.ParametersJson, "Sheet", "StockHeight"):0.###} мм");

        var items = requirement.Items.Select(i =>
        {
            stockLookup.TryGetValue(i.MetalMaterialId, out var stock);
            var lossFactor = i.CoefConsumption <= 0m ? 1m : i.CoefConsumption;
            var qtyToIssue = i.TotalRequiredQty * lossFactor;
            var backMeters = i.TotalRequiredWeightKg.HasValue && i.MassPerMeterKg > 0m
                ? i.TotalRequiredWeightKg.Value / (i.MassPerMeterKg * lossFactor)
                : 0m;
            var backSquareMeters = i.TotalRequiredWeightKg.HasValue && i.MassPerSquareMeterKg > 0m
                ? i.TotalRequiredWeightKg.Value / (i.MassPerSquareMeterKg * lossFactor)
                : 0m;

            return new MetalRequirementDetailsItemViewModel
            {
                MaterialDisplay = string.IsNullOrWhiteSpace(i.MaterialCode) ? i.MaterialName : $"{i.MaterialName} ({i.MaterialCode})",
                MaterialArticle = !string.IsNullOrWhiteSpace(i.MaterialCode) && i.MaterialCode.StartsWith("06.")
                    ? i.MaterialCode
                    : "—",
                NormPerUnit = i.NormPerUnit,
                TotalRequiredQty = i.TotalRequiredQty,
                Unit = i.Unit,
                TotalRequiredWeightKg = i.TotalRequiredWeightKg,
                NetRequirementQty = i.TotalRequiredQty,
                LossFactor = lossFactor,
                QtyToIssueFromStock = qtyToIssue,
                ExpectedBusinessResidual = currentPlan?.BusinessResidual ?? 0m,
                ExpectedScrapResidual = currentPlan?.ScrapResidual ?? 0m,
                SourceBlankDisplay = sourceBlank,
                CuttingPlanId = currentPlan?.Id,
                CalculationFormula = i.CalculationFormula,
                CalculationInput = i.CalculationInput,
                BackCalculatedMeters = backMeters,
                BackCalculatedSquareMeters = backSquareMeters,
                StockQty = stock?.Qty ?? 0m,
                StockWeightKg = stock?.WeightKg ?? 0m,
                SelectionSource = i.SelectionSource,
                SelectionReason = i.SelectionReason,
                CandidateMaterials = i.CandidateMaterials,
            };
        }).ToList();

        return new MetalRequirementDetailsViewModel
        {
            Id = requirement.Id,
            RequirementNumber = requirement.RequirementNumber,
            RequirementDate = requirement.RequirementDate,
            Status = requirement.Status,
            PartDisplay = string.IsNullOrWhiteSpace(requirement.PartCode) ? requirement.PartName : $"{requirement.PartName} ({requirement.PartCode})",
            Quantity = requirement.Quantity,
            WipLaunchId = requirement.WipLaunchId,
            LaunchDate = requirement.LaunchDate,
            Comment = requirement.Comment,
            SourceBlankDisplay = sourceBlank,
            CurrentCuttingPlanId = currentPlan?.Id,
            Aggregates = new MetalRequirementAggregateViewModel
            {
                TotalKg = items.Sum(x => x.TotalRequiredWeightKg ?? 0m),
                TotalMeters = items.Sum(x => x.BackCalculatedMeters),
                TotalSquareMeters = items.Sum(x => x.BackCalculatedSquareMeters),
                ForecastWastePercent = currentPlan?.WastePercent ?? 0m,
                ForecastBusinessResidual = currentPlan?.BusinessResidual ?? 0m,
                ForecastScrapResidual = currentPlan?.ScrapResidual ?? 0m,
            },
            CutDetails = currentPlan?.Items
                .OrderBy(x => x.StockIndex)
                .ThenBy(x => x.Sequence)
                .Select(x => new MetalRequirementCutDetailViewModel
                {
                    StockIndex = x.StockIndex,
                    Sequence = x.Sequence,
                    ItemType = x.ItemType,
                    Length = x.Length,
                    Width = x.Width,
                    Height = x.Height,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotated = x.Rotated,
                    Quantity = x.Quantity,
                })
                .ToList() ?? [],
            Items = items,
        };
    }

    private static CuttingMapCardViewModel MapCuttingPlan(CuttingPlan plan)
    {
        var stockLength = ReadDecimalFromJson(plan.ParametersJson, "Linear", "StockLength");
        var stockWidth = ReadDecimalFromJson(plan.ParametersJson, "Sheet", "StockWidth");
        var stockHeight = ReadDecimalFromJson(plan.ParametersJson, "Sheet", "StockHeight");

        var stocks = plan.Items
            .GroupBy(x => x.StockIndex)
            .OrderBy(x => x.Key)
            .Select(g =>
            {
                var placements = g.OrderBy(x => x.Sequence).ToList();
                var parts = placements.Where(x => x.ItemType == "part" && x.Length.HasValue).Select(x => x.Length!.Value.ToString("0.###")).ToList();
                var residual = placements.FirstOrDefault(x => x.ItemType != "part");
                var stepText = stockLength.HasValue
                    ? $"Хлыст #{g.Key + 1}: {stockLength.Value:0.###} -> {string.Join(" + ", parts)} + остаток {(residual?.Length ?? 0m):0.###} мм"
                    : $"Лист #{g.Key + 1}: {stockWidth:0.###} x {stockHeight:0.###}";

                return new CuttingMapStockViewModel
                {
                    StockIndex = g.Key,
                    StepDescription = stepText,
                    Placements = placements.Select(p => new CuttingMapPlacementViewModel
                    {
                        ItemType = p.ItemType,
                        Length = p.Length,
                        Width = p.Width,
                        Height = p.Height,
                        PositionX = p.PositionX,
                        PositionY = p.PositionY,
                        Rotated = p.Rotated,
                    }).ToList(),
                };
            })
            .ToList();

        var requirement = plan.MetalRequirement;
        var partDisplay = requirement?.Part is null
            ? "—"
            : (string.IsNullOrWhiteSpace(requirement.Part.Code) ? requirement.Part.Name : $"{requirement.Part.Name} ({requirement.Part.Code})");

        return new CuttingMapCardViewModel
        {
            PlanId = plan.Id,
            RequirementId = plan.MetalRequirementId,
            RequirementNumber = requirement?.RequirementNumber ?? "—",
            PartDisplay = partDisplay,
            Kind = plan.Kind == CuttingPlanKind.OneDimensional ? "1D" : "2D",
            Version = plan.Version,
            CreatedAt = plan.CreatedAt,
            UtilizationPercent = plan.UtilizationPercent,
            WastePercent = plan.WastePercent,
            CutCount = plan.CutCount,
            BusinessResidual = plan.BusinessResidual,
            ScrapResidual = plan.ScrapResidual,
            ExecutionStatus = string.IsNullOrWhiteSpace(plan.ExecutionStatus) ? "Не выполнено" : plan.ExecutionStatus,
            ActualResidual = plan.ActualResidual,
            StockCaption = stockLength.HasValue
                ? $"Исходная длина хлыста: {stockLength:0.###} мм"
                : $"Исходный лист: {stockWidth:0.###} x {stockHeight:0.###} мм",
            Stocks = stocks,
        };
    }

    private static decimal? ReadDecimalFromJson(string json, string root, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(root, out var rootElement) || rootElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!rootElement.TryGetProperty(property, out var valueElement))
        {
            return null;
        }

        return valueElement.TryGetDecimal(out var value) ? value : null;
    }

    private static decimal CalculateWeightKg(string profileType, MetalReceiptCreateViewModel model, int quantity, MetalMaterial material)
    {
        return profileType switch
        {
            "sheet" when model.ThicknessMm.HasValue && model.WidthMm.HasValue && model.LengthMm.HasValue =>
                Math.Round(0.00000785m * model.ThicknessMm.Value * model.WidthMm.Value * model.LengthMm.Value * quantity, 3),
            "rod" when model.DiameterMm.HasValue && model.LengthMm.HasValue =>
                Math.Round(0.000006165m * model.DiameterMm.Value * model.DiameterMm.Value * model.LengthMm.Value * quantity, 3),
            "pipe" when model.DiameterMm.HasValue && model.WallThicknessMm.HasValue && model.LengthMm.HasValue =>
                Math.Round(0.00002466m * model.WallThicknessMm.Value * (model.DiameterMm.Value - model.WallThicknessMm.Value) * model.LengthMm.Value * quantity, 3),
            _ => Math.Round((model.ActualWeightKg ?? 0m) * (material.Coefficient <= 0m ? 1m : material.Coefficient), 3),
        };
    }

    private static (decimal SizeValue, string UnitText) ResolveSizeFromInputOrMass(string profileType, decimal? sizeValue, MetalReceiptCreateViewModel model, MetalMaterial material, int quantity, decimal actualWeight)
    {
        if (sizeValue.HasValue && sizeValue.Value > 0m)
        {
            return (sizeValue.Value, profileType == "sheet" ? "м2" : "м");
        }

        var perItemWeight = quantity <= 0 ? 0m : actualWeight / quantity;
        if (profileType == "sheet")
        {
            if (material.MassPerSquareMeterKg > 0m)
            {
                return (Math.Round(perItemWeight / material.MassPerSquareMeterKg, 3), "м2");
            }

            if (model.ThicknessMm.HasValue)
            {
                var areaM2 = (model.ThicknessMm.Value * 7.85m) > 0m ? perItemWeight / (model.ThicknessMm.Value * 7.85m) : 0m;
                return (Math.Round(areaM2, 3), "м2");
            }
        }

        if (material.MassPerMeterKg > 0m)
        {
            return (Math.Round(perItemWeight / material.MassPerMeterKg, 3), "м");
        }

        var fromLength = (model.LengthMm ?? 0m) / 1000m;
        return (Math.Round(fromLength, 3), "м");
    }

    private static string ResolveStockCategory(string profileType, decimal sizeValue)
    {
        if (sizeValue <= 0m)
        {
            return "scrap";
        }

        return profileType switch
        {
            "sheet" when sizeValue >= 1m => "whole",
            "rod" or "pipe" when sizeValue >= 3m => "whole",
            _ when sizeValue >= 0.25m => "business",
            _ => "scrap",
        };
    }

    private static string ResolveProfileType(string materialName, string? selectedType)
    {
        var explicitType = (selectedType ?? string.Empty).Trim().ToLowerInvariant();
        if (explicitType is "sheet" or "rod" or "pipe")
        {
            return explicitType;
        }

        var haystack = materialName.ToLowerInvariant();
        if (haystack.Contains("труб"))
        {
            return "pipe";
        }

        if (haystack.Contains("круг") || haystack.Contains("прут"))
        {
            return "rod";
        }

        return "sheet";
    }

    private static string ToProfileCaption(string profileType) =>
        profileType switch
        {
            "sheet" => "Лист",
            "rod" => "Круг/пруток",
            "pipe" => "Труба",
            _ => profileType,
        };

    private static string ToStockCategoryCaption(string stockCategory) =>
        stockCategory switch
        {
            "whole" => "Целая заготовка",
            "business" => "Деловой остаток",
            "scrap" => "Лом",
            _ => "В наличии",
        };


    private static DateTime ToUtcDate(DateTime value)
    {
        var dateOnly = value.Date;
        return value.Kind switch
        {
            DateTimeKind.Utc => dateOnly,
            DateTimeKind.Local => dateOnly.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc),
        };
    }
    private async Task PopulateMaterialsAsync(MetalReceiptCreateViewModel model, CancellationToken cancellationToken)
    {
        model.Materials = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = x.Id.ToString(),
                Text = string.IsNullOrWhiteSpace(x.Code) ? x.Name : $"{x.Name} ({x.Code})",
                Selected = model.MetalMaterialId.HasValue && model.MetalMaterialId.Value == x.Id,
            })
            .ToListAsync(cancellationToken);

        model.MaterialProfileTypes = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => ResolveProfileType(x.Name, null), cancellationToken);

        if (model.Materials.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Нет доступных материалов для прихода. Добавьте материалы в справочник.");
        }
    }

    private async Task EnsureMetalMaterialsSeededAsync(CancellationToken cancellationToken)
    {
        var hasActiveMaterials = await _dbContext.MetalMaterials.AnyAsync(x => x.IsActive, cancellationToken);
        if (hasActiveMaterials)
        {
            return;
        }

        var seed = new[]
        {
            new MetalMaterial { Id = Guid.NewGuid(), Name = "Лист ст.35 t=6 ГОСТ19903-74/1577-93", Code = "LIST35T6", UnitKind = "SquareMeter", MassPerSquareMeterKg = 1.5m, CoefConsumption = 1m, StockUnit = "m2", WeightPerUnitKg = 1.5m, Coefficient = 1m, IsActive = true },
            new MetalMaterial { Id = Guid.NewGuid(), Name = "Пруток 20Г", Code = "PRUT20G", UnitKind = "Meter", MassPerMeterKg = 0.8m, CoefConsumption = 1m, StockUnit = "m", WeightPerUnitKg = 0.8m, Coefficient = 1m, IsActive = true },
            new MetalMaterial { Id = Guid.NewGuid(), Name = "Круг 45", Code = "KRUG45", UnitKind = "Meter", MassPerMeterKg = 0.9m, CoefConsumption = 1m, StockUnit = "m", WeightPerUnitKg = 0.9m, Coefficient = 1m, IsActive = true },
            new MetalMaterial { Id = Guid.NewGuid(), Name = "Лист 09Г2С t=4", Code = "LIST09G2S4", UnitKind = "SquareMeter", MassPerSquareMeterKg = 1.2m, CoefConsumption = 1m, StockUnit = "m2", WeightPerUnitKg = 1.2m, Coefficient = 1m, IsActive = true },
        };

        _dbContext.MetalMaterials.AddRange(seed);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetNextReceiptNumberAsync(CancellationToken cancellationToken)
    {
        var lastNumber = await _dbContext.MetalReceipts
            .AsNoTracking()
            .Where(x => x.ReceiptNumber.StartsWith("MR-"))
            .OrderByDescending(x => x.ReceiptNumber)
            .Select(x => x.ReceiptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var numericPart = 0;
        if (!string.IsNullOrWhiteSpace(lastNumber) && lastNumber.Length > 3)
        {
            _ = int.TryParse(lastNumber[3..], out numericPart);
        }

        return $"MR-{(numericPart + 1):D6}";
    }

    private static string BuildMaterialCode(MetalMaterial material)
    {
        if (!string.IsNullOrWhiteSpace(material.Code))
        {
            return material.Code.Trim().ToUpperInvariant();
        }

        var letters = new string(material.Name
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(8)
            .ToArray());
        return string.IsNullOrWhiteSpace(letters) ? "METAL" : letters;
    }
}
