using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    private const string PlanDraftStatus = "Draft";
    private const string PlanCalculatedStatus = "Calculated";
    private const string LineStatusFullUse = "FullUse";
    private const string LineStatusPartialCut = "PartialCut";
    private const string LineStatusReserveCandidate = "ReserveCandidate";
    private const string LineStatusDeficit = "Deficit";
    private const string IssueDraftStatus = "Draft";
    private const string IssueCompletedStatus = "Completed";
    private const string IssueCancelledStatus = "Cancelled";
    private const string RequirementCancelledStatus = "Cancelled";
    private const string MovementTypeIssue = "Issue";
    private const string MovementTypeFullConsumption = "FullConsumption";
    private const string MovementTypeResidualUpdate = "ResidualUpdate";

    private readonly AppDbContext _dbContext;
    private readonly ICuttingMapExcelExporter _cuttingMapExcelExporter;
    private readonly ICuttingMapPdfExporter _cuttingMapPdfExporter;
    private readonly IMetalRequirementWarehousePrintDocumentService _requirementWarehousePrintDocumentService;

    public MetalWarehouseController(
        AppDbContext dbContext,
        ICuttingMapExcelExporter cuttingMapExcelExporter,
        ICuttingMapPdfExporter cuttingMapPdfExporter,
        IMetalRequirementWarehousePrintDocumentService requirementWarehousePrintDocumentService)
    {
        _dbContext = dbContext;
        _cuttingMapExcelExporter = cuttingMapExcelExporter;
        _cuttingMapPdfExporter = cuttingMapPdfExporter;
        _requirementWarehousePrintDocumentService = requirementWarehousePrintDocumentService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new MetalWarehouseDashboardViewModel
        {
            MaterialsInCatalog = await _dbContext.Parts.AsNoTracking().CountAsync(cancellationToken),
            MetalUnitsInStock = await _dbContext.WarehouseItems.AsNoTracking().SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m,
            OpenRequirements = await _dbContext.MetalRequirements.AsNoTracking().CountAsync(x => x.Status == "Created", cancellationToken),
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
            Comment = model.Comment?.Trim(),
            CreatedAt = now,
        };

        var profileType = ResolveProfileType(material.Name, model.ProfileType);
        var materialCode = BuildMaterialCode(material);
        var quantity = model.Quantity!.Value;
        var passportWeight = model.PassportWeightKg!.Value;
        var actualWeight = passportWeight;
        var calculatedWeight = CalculateWeightKg(profileType, model, quantity, material);
        var deviation = actualWeight - passportWeight;

        if (!ModelState.IsValid)
        {
            await PopulateMaterialsAsync(model, cancellationToken);
            return View("~/Views/MetalWarehouse/CreateReceipt.cshtml", model);
        }

        receipt.BatchNumber = string.Empty;
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
            Comment = receipt.Comment,
            MaterialName = first.MaterialName,
            PassportWeightKg = first.PassportWeightKg,
            CalculatedWeightKg = first.CalculatedWeightKg,
            WeightDeviationKg = first.WeightDeviationKg,
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
        var onlyActive = !Request.Query.ContainsKey(nameof(MetalStockFilterViewModel.ActiveOnly)) || filter.ActiveOnly;
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
                x.IsConsumed,
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

        if (onlyActive)
        {
            stockQuery = stockQuery.Where(x => !x.IsConsumed);
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
                ActiveOnly = onlyActive,
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
                StockCategory = x.StockCategory,
                Status = x.IsConsumed ? "Израсходовано" : ToStockCategoryCaption(x.StockCategory),
            }).ToList(),
            TotalUnitsCount = stockRows.Count,
            TotalMaterialsCount = stockRows.Select(x => x.MetalMaterialId).Distinct().Count(),
            TotalWeightKg = stockRows.Sum(x => x.WeightKg),
            TotalSize = stockRows.Sum(x => x.SizeValue),
        };

        return View(model);
    }

    [HttpGet("CuttingReports")]
    public async Task<IActionResult> CuttingReports(CancellationToken cancellationToken)
    {
        var plans = await _dbContext.CuttingPlans
            .AsNoTracking()
            .Where(x => x.IsCurrent)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{(x.MetalRequirement != null ? x.MetalRequirement.RequirementNumber : "—")} · v{x.Version}",
            })
            .ToListAsync(cancellationToken);

        var sourceItems = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Where(x => !x.IsConsumed)
            .OrderBy(x => x.GeneratedCode)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.GeneratedCode} · {x.SizeValue:0.###} {x.SizeUnitText}",
            })
            .ToListAsync(cancellationToken);

        var reports = await _dbContext.CuttingReports
            .AsNoTracking()
            .OrderByDescending(x => x.ReportDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new CuttingReportListItemViewModel
            {
                Id = x.Id,
                ReportNumber = x.ReportNumber,
                ReportDate = x.ReportDate,
                RequirementNumber = x.CuttingPlan != null && x.CuttingPlan.MetalRequirement != null ? x.CuttingPlan.MetalRequirement.RequirementNumber : "—",
                MaterialName = x.SourceMetalReceiptItem != null && x.SourceMetalReceiptItem.MetalMaterial != null ? x.SourceMetalReceiptItem.MetalMaterial.Name : "—",
                SourceCode = x.SourceMetalReceiptItem != null ? x.SourceMetalReceiptItem.GeneratedCode : "—",
                Workshop = x.Workshop,
                Shift = x.Shift,
                PlannedSize = x.PlannedSize,
                ActualProducedSize = x.ActualProducedSize,
                PlannedMassKg = x.PlannedMassKg,
                ActualProducedMassKg = x.ActualProducedMassKg,
                PlannedWaste = x.PlannedWaste,
                ActualWaste = x.ActualWaste,
            })
            .ToListAsync(cancellationToken);

        var analytics = reports
            .GroupBy(x => new { x.Workshop, x.Shift, x.MaterialName })
            .Select(g => new CuttingAnalyticsItemViewModel
            {
                Workshop = g.Key.Workshop,
                Shift = g.Key.Shift,
                MaterialName = g.Key.MaterialName,
                ReportsCount = g.Count(),
                AvgWasteDeviation = g.Average(x => x.WasteDeviation),
                TotalScrapMassKg = g.Sum(x => x.ActualWaste),
            })
            .OrderByDescending(x => x.TotalScrapMassKg)
            .ToList();

        var model = new CuttingReportPageViewModel
        {
            PlanOptions = plans,
            SourceOptions = sourceItems,
            Reports = reports,
            Analytics = analytics,
            CreateModel = new CuttingReportCreateViewModel(),
        };

        return View("~/Views/MetalWarehouse/CuttingReports.cshtml", model);
    }

    [HttpPost("CuttingReports")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCuttingReport(CuttingReportCreateViewModel model, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.CuttingPlans
            .Include(x => x.MetalRequirement)
            .FirstOrDefaultAsync(x => x.Id == model.CuttingPlanId, cancellationToken);
        var source = await _dbContext.MetalReceiptItems
            .Include(x => x.MetalMaterial)
            .FirstOrDefaultAsync(x => x.Id == model.SourceMetalReceiptItemId, cancellationToken);

        if (plan is null)
        {
            ModelState.AddModelError(nameof(model.CuttingPlanId), "План раскроя не найден.");
        }

        if (source is null || source.IsConsumed)
        {
            ModelState.AddModelError(nameof(model.SourceMetalReceiptItemId), "Исходная заготовка не найдена или уже списана.");
        }

        if (model.ActualProducedSize + model.BusinessResidualSize + model.ScrapSize <= 0m)
        {
            ModelState.AddModelError(string.Empty, "Укажите результат раскроя (полезный выход/остатки/лом).");
        }

        if (!ModelState.IsValid)
        {
            return await CuttingReports(cancellationToken);
        }

        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(source);

        var now = DateTime.UtcNow;
        var report = new CuttingReport
        {
            Id = Guid.NewGuid(),
            ReportNumber = await GetNextCuttingReportNumberAsync(cancellationToken),
            ReportDate = now.Date,
            CuttingPlanId = plan.Id,
            SourceMetalReceiptItemId = source.Id,
            Workshop = model.Workshop.Trim(),
            Shift = model.Shift.Trim(),
            PlannedSize = source.SizeValue - plan.BusinessResidual - plan.ScrapResidual,
            ActualProducedSize = model.ActualProducedSize,
            PlannedMassKg = source.Quantity > 0 ? source.TotalWeightKg / source.Quantity : source.TotalWeightKg,
            ActualProducedMassKg = model.ActualProducedMassKg,
            PlannedWaste = plan.ScrapResidual,
            ActualWaste = model.ScrapMassKg,
            BusinessResidual = model.BusinessResidualSize,
            ScrapSize = model.ScrapSize,
            ScrapMassKg = model.ScrapMassKg,
            CreatedAt = now,
        };

        source.IsConsumed = true;
        source.ConsumedAt = now;
        source.ConsumedByCuttingReportId = report.Id;

        if (model.BusinessResidualSize > 0m)
        {
            _dbContext.MetalReceiptItems.Add(new MetalReceiptItem
            {
                Id = Guid.NewGuid(),
                MetalReceiptId = source.MetalReceiptId,
                MetalMaterialId = source.MetalMaterialId,
                Quantity = 1m,
                TotalWeightKg = model.BusinessResidualMassKg,
                ItemIndex = source.ItemIndex,
                SizeValue = model.BusinessResidualSize,
                SizeUnitText = source.SizeUnitText,
                ProfileType = source.ProfileType,
                ThicknessMm = source.ThicknessMm,
                WidthMm = source.WidthMm,
                LengthMm = source.LengthMm,
                DiameterMm = source.DiameterMm,
                WallThicknessMm = source.WallThicknessMm,
                PassportWeightKg = model.BusinessResidualMassKg,
                ActualWeightKg = model.BusinessResidualMassKg,
                CalculatedWeightKg = model.BusinessResidualMassKg,
                WeightDeviationKg = 0m,
                StockCategory = "business",
                GeneratedCode = $"{source.GeneratedCode}-BUS",
                SourceCuttingReportId = report.Id,
                CreatedAt = now,
            });
        }

        if (model.ScrapSize > 0m || model.ScrapMassKg > 0m)
        {
            _dbContext.MetalReceiptItems.Add(new MetalReceiptItem
            {
                Id = Guid.NewGuid(),
                MetalReceiptId = source.MetalReceiptId,
                MetalMaterialId = source.MetalMaterialId,
                Quantity = 1m,
                TotalWeightKg = model.ScrapMassKg,
                ItemIndex = source.ItemIndex,
                SizeValue = model.ScrapSize,
                SizeUnitText = source.SizeUnitText,
                ProfileType = source.ProfileType,
                ThicknessMm = source.ThicknessMm,
                WidthMm = source.WidthMm,
                LengthMm = source.LengthMm,
                DiameterMm = source.DiameterMm,
                WallThicknessMm = source.WallThicknessMm,
                PassportWeightKg = model.ScrapMassKg,
                ActualWeightKg = model.ScrapMassKg,
                CalculatedWeightKg = model.ScrapMassKg,
                WeightDeviationKg = 0m,
                StockCategory = "scrap",
                GeneratedCode = $"{source.GeneratedCode}-SCRAP",
                SourceCuttingReportId = report.Id,
                CreatedAt = now,
            });
        }

        plan.ExecutionStatus = "выполнено";
        plan.ActualResidual = model.BusinessResidualSize + model.ScrapSize;
        _dbContext.CuttingReports.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(CuttingReports));
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
                Comment = x.MetalReceipt != null ? x.MetalReceipt.Comment : null,
                CreatedAt = x.CreatedAt,
                x.IsConsumed,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        var movements = await _dbContext.MetalStockMovements
            .AsNoTracking()
            .Where(x => x.MetalReceiptItemId == id)
            .OrderBy(x => x.MovementDate)
            .Select(x => new
            {
                x.MovementDate,
                x.MovementType,
                x.QtyBefore,
                x.QtyChange,
                x.QtyAfter,
                x.Unit,
                x.Comment,
            })
            .ToListAsync(cancellationToken);

        var history = new List<MetalStockItemHistoryEntryViewModel>
        {
            new()
            {
                Timestamp = item.CreatedAt,
                EventName = "Приход",
                Description = $"Единица добавлена по документу {item.ReceiptNumber}.",
            },
        };

        history.AddRange(movements.Select(x => new MetalStockItemHistoryEntryViewModel
        {
            Timestamp = x.MovementDate,
            EventName = x.MovementType switch
            {
                MovementTypeIssue => "Выдача по требованию",
                MovementTypeResidualUpdate => "Изменение остатка",
                MovementTypeFullConsumption => "Полный расход",
                _ => "Движение",
            },
            Description = $"{(x.QtyBefore ?? 0m):0.###} -> {(x.QtyAfter ?? 0m):0.###} {x.Unit} (изменение {x.QtyChange:0.###}). {(string.IsNullOrWhiteSpace(x.Comment) ? string.Empty : x.Comment)}".Trim(),
        }));

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
            ReceiptComment = item.Comment,
            Status = item.IsConsumed ? "Израсходовано" : "В наличии",
            History = history,
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
            .Select(x => new MetalRequirementListItemViewModel
            {
                Id = x.Id,
                RequirementNumber = x.RequirementNumber,
                RequirementDate = x.RequirementDate,
                PartDisplay = string.IsNullOrWhiteSpace(x.PartCode)
                    ? x.PartName
                    : $"{x.PartName} ({x.PartCode})",
                Quantity = x.Quantity,
                MaterialDisplay = x.MetalMaterial != null
                    ? (string.IsNullOrWhiteSpace(x.MetalMaterial.Code)
                        ? x.MetalMaterial.Name
                        : $"{x.MetalMaterial.Name} ({x.MetalMaterial.Code})")
                    : "—",
                RequiredQty = x.Items.Select(i => (decimal?)i.RequiredQty).FirstOrDefault() ?? 0m,
                Unit = x.Items.Select(i => i.ConsumptionUnit).FirstOrDefault() ?? string.Empty,
                Status = x.Status,
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

    [HttpPost("Requirements/Details/{id:guid}/issues/create-from-plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIssueFromPlan(Guid id, CancellationToken cancellationToken)
    {
        var requirement = await _dbContext.MetalRequirements
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (requirement is null)
        {
            return NotFound();
        }

        if (requirement.Status == IssueCompletedStatus)
        {
            TempData["MetalRequirementError"] = "Требование уже исполнено. Повторное оформление выдачи запрещено.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }

        if (requirement.Status == RequirementCancelledStatus)
        {
            TempData["MetalRequirementError"] = "Требование отменено. Оформление выдачи запрещено.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }

        var existingIssue = await _dbContext.MetalIssues
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MetalRequirementId == id && x.Status != IssueCancelledStatus, cancellationToken);
        if (existingIssue is not null)
        {
            return RedirectToAction(nameof(IssueDetails), new { id = existingIssue.Id });
        }

        var plan = await _dbContext.MetalRequirementPlans
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.MetalRequirementId == id, cancellationToken);
        if (plan is null || plan.Items.Count == 0)
        {
            TempData["MetalRequirementError"] = "Невозможно оформить выдачу: план подбора не рассчитан.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }

        if (plan.DeficitQty > 0m)
        {
            var deficitUnit = requirement.Items.FirstOrDefault()?.Unit ?? "мм";
            TempData["MetalRequirementError"] = $"Невозможно оформить выдачу: по плану есть дефицит {plan.DeficitQty:0.###} {deficitUnit}.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }

        var issue = new MetalIssue
        {
            Id = Guid.NewGuid(),
            MetalRequirementId = id,
            IssueNumber = await GetNextIssueNumberAsync(cancellationToken),
            IssueDate = DateTime.UtcNow,
            Status = IssueDraftStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User?.Identity?.Name ?? "system",
            Comment = "Черновик выдачи создан из плана подбора.",
            Items = plan.Items
                .Where(x => x.MetalReceiptItemId.HasValue && x.PlannedUseQty > 0m && x.LineStatus != LineStatusDeficit)
                .OrderBy(x => x.SortOrder)
                .Select(x => new MetalIssueItem
                {
                    Id = Guid.NewGuid(),
                    MetalReceiptItemId = x.MetalReceiptItemId!.Value,
                    SourceCode = x.SourceCode,
                    SourceQtyBefore = x.SourceSize,
                    IssuedQty = x.PlannedUseQty,
                    RemainingQtyAfter = x.RemainingAfterQty,
                    Unit = x.SourceUnit,
                    LineStatus = x.LineStatus,
                    SortOrder = x.SortOrder,
                })
                .ToList(),
        };

        if (issue.Items.Count == 0)
        {
            TempData["MetalRequirementError"] = "План не содержит строк для фактической выдачи.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }

        _dbContext.MetalIssues.Add(issue);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(IssueDetails), new { id = issue.Id });
    }

    [HttpGet("Issues")]
    public async Task<IActionResult> Issues(CancellationToken cancellationToken)
    {
        var items = await _dbContext.MetalIssues
            .AsNoTracking()
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new MetalIssueListItemViewModel
            {
                Id = x.Id,
                IssueNumber = x.IssueNumber,
                IssueDate = x.IssueDate,
                RequirementNumber = x.MetalRequirement != null ? x.MetalRequirement.RequirementNumber : "—",
                PartDisplay = x.MetalRequirement == null
                    ? "—"
                    : (string.IsNullOrWhiteSpace(x.MetalRequirement.PartCode)
                        ? x.MetalRequirement.PartName
                        : $"{x.MetalRequirement.PartName} ({x.MetalRequirement.PartCode})"),
                MaterialDisplay = x.MetalRequirement != null && x.MetalRequirement.MetalMaterial != null
                    ? (string.IsNullOrWhiteSpace(x.MetalRequirement.MetalMaterial.Code)
                        ? x.MetalRequirement.MetalMaterial.Name
                        : $"{x.MetalRequirement.MetalMaterial.Name} ({x.MetalRequirement.MetalMaterial.Code})")
                    : "—",
                Status = x.Status,
            })
            .ToListAsync(cancellationToken);

        return View("~/Views/MetalWarehouse/Issues.cshtml", new MetalIssueListViewModel
        {
            Items = items,
        });
    }

    [HttpGet("Issues/Details/{id:guid}")]
    public async Task<IActionResult> IssueDetails(Guid id, CancellationToken cancellationToken)
    {
        var issue = await _dbContext.MetalIssues
            .AsNoTracking()
            .Include(x => x.MetalRequirement)
                .ThenInclude(x => x!.MetalMaterial)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (issue is null)
        {
            return NotFound();
        }

        return View("~/Views/MetalWarehouse/IssueDetails.cshtml", new MetalIssueDetailsViewModel
        {
            Id = issue.Id,
            IssueNumber = issue.IssueNumber,
            IssueDate = issue.IssueDate,
            Status = issue.Status,
            RequirementId = issue.MetalRequirementId,
            RequirementNumber = issue.MetalRequirement?.RequirementNumber ?? "—",
            PartDisplay = issue.MetalRequirement is null
                ? "—"
                : (string.IsNullOrWhiteSpace(issue.MetalRequirement.PartCode)
                    ? issue.MetalRequirement.PartName
                    : $"{issue.MetalRequirement.PartName} ({issue.MetalRequirement.PartCode})"),
            MaterialDisplay = issue.MetalRequirement?.MetalMaterial is null
                ? "—"
                : (string.IsNullOrWhiteSpace(issue.MetalRequirement.MetalMaterial.Code)
                    ? issue.MetalRequirement.MetalMaterial.Name
                    : $"{issue.MetalRequirement.MetalMaterial.Name} ({issue.MetalRequirement.MetalMaterial.Code})"),
            Quantity = issue.MetalRequirement?.Quantity ?? 0m,
            Comment = issue.Comment,
            CreatedAt = issue.CreatedAt,
            CreatedBy = issue.CreatedBy,
            CompletedAt = issue.CompletedAt,
            CompletedBy = issue.CompletedBy,
            CanComplete = issue.Status == IssueDraftStatus,
            Items = issue.Items
                .OrderBy(x => x.SortOrder)
                .Select(x => new MetalIssueDetailsItemViewModel
                {
                    SourceCode = x.SourceCode,
                    SourceQtyBefore = x.SourceQtyBefore,
                    IssuedQty = x.IssuedQty,
                    RemainingQtyAfter = x.RemainingQtyAfter,
                    Unit = x.Unit,
                    LineStatus = x.LineStatus,
                })
                .ToList(),
        });
    }

    [HttpPost("Issues/Details/{id:guid}/complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteIssue(Guid id, CancellationToken cancellationToken)
    {
        var issue = await _dbContext.MetalIssues
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (issue is null)
        {
            return NotFound();
        }

        if (issue.Status == IssueCompletedStatus)
        {
            TempData["MetalIssueError"] = "Эта выдача уже проведена.";
            return RedirectToAction(nameof(IssueDetails), new { id });
        }

        var requirement = await _dbContext.MetalRequirements
            .FirstOrDefaultAsync(x => x.Id == issue.MetalRequirementId, cancellationToken);
        if (requirement is null)
        {
            return NotFound();
        }

        if (requirement.Status == IssueCompletedStatus)
        {
            TempData["MetalIssueError"] = "Требование уже исполнено другой выдачей. Повторное проведение запрещено.";
            return RedirectToAction(nameof(IssueDetails), new { id });
        }

        if (requirement.Status == RequirementCancelledStatus)
        {
            TempData["MetalIssueError"] = "Требование отменено. Проведение выдачи запрещено.";
            return RedirectToAction(nameof(IssueDetails), new { id });
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var userName = User?.Identity?.Name ?? "system";
        foreach (var line in issue.Items.OrderBy(x => x.SortOrder))
        {
            var source = await _dbContext.MetalReceiptItems
                .FirstOrDefaultAsync(x => x.Id == line.MetalReceiptItemId, cancellationToken);
            if (source is null)
            {
                await tx.RollbackAsync(cancellationToken);
                TempData["MetalIssueError"] = $"Не найдена исходная единица {line.SourceCode}.";
                return RedirectToAction(nameof(IssueDetails), new { id });
            }

            if (source.IsConsumed)
            {
                await tx.RollbackAsync(cancellationToken);
                TempData["MetalIssueError"] = $"Единица {source.GeneratedCode} уже недоступна для выдачи.";
                return RedirectToAction(nameof(IssueDetails), new { id });
            }

            if (source.SizeValue < line.IssuedQty)
            {
                await tx.RollbackAsync(cancellationToken);
                TempData["MetalIssueError"] = $"Недостаточный остаток по {source.GeneratedCode}: доступно {source.SizeValue:0.###} {source.SizeUnitText}, требуется {line.IssuedQty:0.###} {line.Unit}.";
                return RedirectToAction(nameof(IssueDetails), new { id });
            }

            var qtyBefore = source.SizeValue;
            if (line.IssuedQty == qtyBefore)
            {
                source.IsConsumed = true;
                source.ConsumedAt = now;
                source.SizeValue = 0m;
                line.RemainingQtyAfter = 0m;
                line.LineStatus = LineStatusFullUse;
            }
            else
            {
                source.SizeValue = line.RemainingQtyAfter;
                line.LineStatus = LineStatusPartialCut;
            }

            _dbContext.MetalStockMovements.Add(new MetalStockMovement
            {
                Id = Guid.NewGuid(),
                MovementDate = now,
                MovementType = line.LineStatus == LineStatusPartialCut ? MovementTypeResidualUpdate : MovementTypeFullConsumption,
                MetalMaterialId = source.MetalMaterialId,
                MetalReceiptItemId = source.Id,
                SourceDocumentType = "MetalIssue",
                SourceDocumentId = issue.Id,
                QtyBefore = qtyBefore,
                QtyChange = -line.IssuedQty,
                QtyAfter = line.RemainingQtyAfter,
                Unit = source.SizeUnitText,
                Comment = $"Выдача по требованию {requirement.RequirementNumber}.",
                CreatedAt = now,
                CreatedBy = userName,
            });
        }

        issue.Status = IssueCompletedStatus;
        issue.CompletedAt = now;
        issue.CompletedBy = userName;
        requirement.Status = IssueCompletedStatus;
        requirement.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return RedirectToAction(nameof(IssueDetails), new { id });
    }

    [HttpPost("Requirements/Details/{id:guid}/plan/calculate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CalculateRequirementPlan(Guid id, CancellationToken cancellationToken)
    {
        var requirement = await _dbContext.MetalRequirements
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (requirement is null)
        {
            return NotFound();
        }

        var existingPlan = await _dbContext.MetalRequirementPlans
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.MetalRequirementId == id, cancellationToken);

        var now = DateTime.UtcNow;
        var userName = User?.Identity?.Name ?? "system";
        var unit = requirement.Items.FirstOrDefault()?.Unit ?? "мм";
        var baseRequiredQty = requirement.Items.Sum(x => x.RequiredQty);
        var adjustedRequiredQty = requirement.Items.Sum(x => x.TotalRequiredQty > 0m ? x.TotalRequiredQty : x.RequiredQty);
        var requiredQty = adjustedRequiredQty;

        var receiptItems = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Where(x => requirement.Items.Select(i => i.MetalMaterialId).Contains(x.MetalMaterialId) && !x.IsConsumed && x.SizeValue > 0m)
            .Select(x => new
            {
                x.Id,
                x.GeneratedCode,
                x.SizeValue,
                x.SizeUnitText,
                UnitWeightKg = x.Quantity > 0 ? x.TotalWeightKg / x.Quantity : x.TotalWeightKg,
                ReceiptDate = x.MetalReceipt != null ? x.MetalReceipt.ReceiptDate : x.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var sortedSources = receiptItems
            .OrderBy(x => Math.Abs(x.SizeValue - requiredQty))
            .ThenBy(x => x.ReceiptDate)
            .ToList();

        var remainingRequired = requiredQty;
        var planItems = new List<MetalRequirementPlanItem>();
        var order = 1;

        foreach (var source in sortedSources)
        {
            if (remainingRequired <= 0m)
            {
                planItems.Add(new MetalRequirementPlanItem
                {
                    Id = Guid.NewGuid(),
                    MetalReceiptItemId = source.Id,
                    SourceCode = source.GeneratedCode,
                    SourceSize = source.SizeValue,
                    SourceUnit = source.SizeUnitText,
                    SourceWeightKg = source.UnitWeightKg,
                    PlannedUseQty = 0m,
                    RemainingAfterQty = source.SizeValue,
                    LineStatus = LineStatusReserveCandidate,
                    SortOrder = order++,
                });
                continue;
            }

            var plannedUse = Math.Min(source.SizeValue, remainingRequired);
            var remainingAfter = source.SizeValue - plannedUse;
            planItems.Add(new MetalRequirementPlanItem
            {
                Id = Guid.NewGuid(),
                MetalReceiptItemId = source.Id,
                SourceCode = source.GeneratedCode,
                SourceSize = source.SizeValue,
                SourceUnit = source.SizeUnitText,
                SourceWeightKg = source.UnitWeightKg,
                PlannedUseQty = plannedUse,
                RemainingAfterQty = remainingAfter,
                LineStatus = remainingAfter > 0m ? LineStatusPartialCut : LineStatusFullUse,
                SortOrder = order++,
            });

            remainingRequired -= plannedUse;
        }

        if (remainingRequired > 0m)
        {
            planItems.Add(new MetalRequirementPlanItem
            {
                Id = Guid.NewGuid(),
                MetalReceiptItemId = null,
                SourceCode = "Дефицит",
                SourceSize = 0m,
                SourceUnit = unit,
                PlannedUseQty = remainingRequired,
                RemainingAfterQty = 0m,
                LineStatus = LineStatusDeficit,
                SortOrder = order,
            });
        }

        var plannedQty = planItems.Where(x => x.LineStatus != LineStatusReserveCandidate && x.LineStatus != LineStatusDeficit).Sum(x => x.PlannedUseQty);
        var deficitQty = Math.Max(0m, requiredQty - plannedQty);

        if (existingPlan is null)
        {
            existingPlan = new MetalRequirementPlan
            {
                Id = Guid.NewGuid(),
                MetalRequirementId = id,
                CreatedAt = now,
                CreatedBy = userName,
            };
            _dbContext.MetalRequirementPlans.Add(existingPlan);
        }
        else
        {
            _dbContext.MetalRequirementPlanItems.RemoveRange(existingPlan.Items);
            existingPlan.RecalculatedAt = now;
            existingPlan.RecalculatedBy = userName;
        }

        existingPlan.Status = PlanCalculatedStatus;
        existingPlan.BaseRequiredQty = baseRequiredQty;
        existingPlan.AdjustedRequiredQty = adjustedRequiredQty;
        existingPlan.PlannedQty = plannedQty;
        existingPlan.DeficitQty = deficitQty;
        existingPlan.CalculationComment = deficitQty > 0m
            ? $"Не хватает {deficitQty:0.###} {unit}."
            : "План рассчитан без дефицита.";
        existingPlan.Items = planItems;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(RequirementDetails), new { id });
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

    [HttpGet("Requirements/Print/{id:guid}")]
    public async Task<IActionResult> PrintRequirement(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор требования.");
        }

        try
        {
            var document = await _requirementWarehousePrintDocumentService.BuildAsync(id, cancellationToken);
            return File(
                document.Content,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                document.FileName);
        }
        catch (FileNotFoundException)
        {
            TempData["MetalRequirementError"] = "Шаблон печатной формы не найден. Проверьте наличие файла Templates/Documents/Требование на склад.docx.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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

    private async Task<string> GetNextCuttingReportNumberAsync(CancellationToken cancellationToken)
    {
        var lastNumber = await _dbContext.CuttingReports
            .AsNoTracking()
            .Where(x => x.ReportNumber.StartsWith("CUTREP-"))
            .OrderByDescending(x => x.ReportNumber)
            .Select(x => x.ReportNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(lastNumber))
        {
            return "CUTREP-0001";
        }

        var chunk = lastNumber["CUTREP-".Length..];
        return int.TryParse(chunk, out var number)
            ? $"CUTREP-{number + 1:D4}"
            : "CUTREP-0001";
    }

    private async Task<string> GetNextIssueNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"MI-{year}-";
        var lastNumber = await _dbContext.MetalIssues
            .AsNoTracking()
            .Where(x => x.IssueNumber.StartsWith(prefix))
            .OrderByDescending(x => x.IssueNumber)
            .Select(x => x.IssueNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(lastNumber))
        {
            return $"{prefix}000001";
        }

        var chunk = lastNumber[prefix.Length..];
        return int.TryParse(chunk, out var number)
            ? $"{prefix}{number + 1:D6}"
            : $"{prefix}000001";
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
                x.PartCode,
                x.PartName,
                x.Quantity,
                x.WipLaunchId,
                LaunchDate = x.WipLaunch != null ? x.WipLaunch.LaunchDate : (DateTime?)null,
                x.Comment,
                MaterialName = x.MetalMaterial != null ? x.MetalMaterial.Name : "—",
                MaterialCode = x.MetalMaterial != null ? x.MetalMaterial.Code : null,
                Items = x.Items.Select(i => new RequirementDetailsItemProjection
                {
                    MetalMaterialId = i.MetalMaterialId,
                    ConsumptionPerUnit = i.ConsumptionPerUnit,
                    ConsumptionUnit = i.ConsumptionUnit,
                    Unit = i.Unit,
                    RequiredQty = i.RequiredQty,
                    RequiredWeightKg = i.RequiredWeightKg,
                    SizeRaw = i.SizeRaw,
                    Comment = i.Comment,
                    MaterialName = i.MetalMaterial != null ? i.MetalMaterial.Name : "—",
                    MaterialCode = i.MetalMaterial != null ? i.MetalMaterial.Code : null,
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

        var requirementPlan = await _dbContext.MetalRequirementPlans
            .AsNoTracking()
                        .Where(x => x.MetalRequirementId == id)
            .OrderByDescending(x => x.RecalculatedAt ?? x.CreatedAt)
            .Include(x => x.Items)
                .ThenInclude(x => x.MetalReceiptItem)
                    .ThenInclude(x => x!.MetalReceipt)
            .FirstOrDefaultAsync(cancellationToken);

        var existingIssue = await _dbContext.MetalIssues
            .AsNoTracking()
            .Where(x => x.MetalRequirementId == id && x.Status != IssueCancelledStatus)
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

        var items = requirement.Items.Select(i => new MetalRequirementDetailsItemViewModel
        {
            MaterialDisplay = string.IsNullOrWhiteSpace(i.MaterialCode) ? i.MaterialName : $"{i.MaterialName} ({i.MaterialCode})",
            NormPerUnit = i.ConsumptionPerUnit,
            Unit = i.ConsumptionUnit,
            TotalRequiredQty = i.RequiredQty,
            TotalRequiredWeightKg = i.RequiredWeightKg,
            SourceBlankDisplay = string.IsNullOrWhiteSpace(i.SizeRaw) ? "—" : i.SizeRaw,
            SelectionReason = i.Comment,
        }).ToList();

        var model = new MetalRequirementDetailsViewModel
        {
            Id = requirement.Id,
            RequirementNumber = requirement.RequirementNumber,
            RequirementDate = requirement.RequirementDate,
            Status = requirement.Status,
            PartDisplay = string.IsNullOrWhiteSpace(requirement.PartCode) ? requirement.PartName : $"{requirement.PartName} ({requirement.PartCode})",
            MaterialDisplay = string.IsNullOrWhiteSpace(requirement.MaterialCode) ? requirement.MaterialName : $"{requirement.MaterialName} ({requirement.MaterialCode})",
            Quantity = requirement.Quantity,
            WipLaunchId = requirement.WipLaunchId,
            LaunchDate = requirement.LaunchDate,
            Comment = requirement.Comment,
            SourceBlankDisplay = sourceBlank,
            CurrentCuttingPlanId = currentPlan?.Id,
            RequirementPlan = requirementPlan is null
                ? null
                : new MetalRequirementPlanViewModel
                {
                    Id = requirementPlan.Id,
                    Status = requirementPlan.Status,
                    BaseRequiredQty = requirementPlan.BaseRequiredQty,
                    AdjustedRequiredQty = requirementPlan.AdjustedRequiredQty,
                    PlannedQty = requirementPlan.PlannedQty,
                    DeficitQty = requirementPlan.DeficitQty,
                    Unit = requirement.Items.FirstOrDefault()?.Unit ?? "мм",
                    CalculationComment = requirementPlan.CalculationComment,
                    CreatedAt = requirementPlan.CreatedAt,
                    CreatedBy = requirementPlan.CreatedBy,
                    RecalculatedAt = requirementPlan.RecalculatedAt,
                    RecalculatedBy = requirementPlan.RecalculatedBy,
                    Items = requirementPlan.Items
                        .OrderBy(x => x.SortOrder)
                        .Select(x => new MetalRequirementPlanItemViewModel
                        {
                            MetalReceiptItemId = x.MetalReceiptItemId,
                            SourceCode = x.SourceCode,
                            SourceSize = x.SourceSize,
                            SourceUnit = x.SourceUnit,
                            PlannedUseQty = x.PlannedUseQty,
                            RemainingAfterQty = x.RemainingAfterQty,
                            LineStatus = x.LineStatus,
                            ReceiptDate = x.MetalReceiptItem?.MetalReceipt?.ReceiptDate,
                        })
                        .ToList(),
                },
            ExistingIssueId = existingIssue?.Id,
            ExistingIssueStatus = existingIssue?.Status,
            HasSelectionPlan = requirementPlan is not null,
            PlanDeficitQty = requirementPlan?.DeficitQty ?? 0m,
            PlanUnit = requirement.Items.FirstOrDefault()?.Unit ?? "мм",
            Aggregates = new MetalRequirementAggregateViewModel
            {
                TotalKg = items.Sum(x => x.TotalRequiredWeightKg ?? 0m),
                TotalMeters = 0m,
                TotalSquareMeters = 0m,
                ForecastWastePercent = 0m,
                ForecastBusinessResidual = 0m,
                ForecastScrapResidual = 0m,
            },
            Items = items,
            CutDetails = [],
        };

        if (existingIssue is not null)
        {
            return model;
        }

        if (requirementPlan is null)
        {
            model.IssueCreationBlockedReason = "Сначала рассчитайте план подбора.";
            return model;
        }

        if (requirementPlan.DeficitQty > 0m)
        {
            model.IssueCreationBlockedReason = $"Выдача невозможна: по плану дефицит {requirementPlan.DeficitQty:0.###} {model.PlanUnit}.";
            return model;
        }

        if (requirement.Status == IssueCompletedStatus)
        {
            model.IssueCreationBlockedReason = "Требование уже исполнено.";
            return model;
        }

        if (requirement.Status == RequirementCancelledStatus)
        {
            model.IssueCreationBlockedReason = "Требование отменено.";
            return model;
        }

        model.CanCreateIssueFromPlan = true;
        return model;
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
            _ => Math.Round((model.PassportWeightKg ?? 0m) * (material.Coefficient <= 0m ? 1m : material.Coefficient), 3),
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

internal sealed class RequirementDetailsItemProjection
{
    public Guid MetalMaterialId { get; init; }

    public decimal ConsumptionPerUnit { get; init; }

    public string ConsumptionUnit { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public decimal RequiredQty { get; init; }

    public decimal? RequiredWeightKg { get; init; }

    public string? SizeRaw { get; init; }

    public string? Comment { get; init; }

    public string MaterialName { get; init; } = string.Empty;

    public string? MaterialCode { get; init; }
}
