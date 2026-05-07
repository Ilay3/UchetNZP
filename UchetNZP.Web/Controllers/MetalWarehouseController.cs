using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
using System.Text.Json;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace UchetNZP.Web.Controllers;

[Route("MetalWarehouse")]
public class MetalWarehouseController : Controller
{
    private const string PlanDraftStatus = "Draft";
    private const string PlanCalculatedStatus = "Calculated";
    private const string PlanHasDeficitStatus = "HasDeficit";
    private const string LineStatusFullUse = "FullUse";
    private const string LineStatusPartialCut = "PartialCut";
    private const string LineStatusReserveCandidate = "ReserveCandidate";
    private const string LineStatusDeficit = "Deficit";
    private const string IssueDraftStatus = "Draft";
    private const string IssueCompletedStatus = "Completed";
    private const string IssueCancelledStatus = "Cancelled";
    private const string RequirementCancelledStatus = "Cancelled";
    private const string RequirementStatusUpdated = "Updated";
    private const string RequirementPlannedStatus = "Planned";
    private const string RequirementReadyToIssueStatus = "ReadyToIssue";
    private const string MovementTypeIssue = "Issue";
    private const string MovementTypeFullConsumption = "FullConsumption";
    private const string MovementTypeResidualUpdate = "ResidualUpdate";
    private const string AuditEventReceiptCreated = "ReceiptCreated";
    private const string AuditEventRequirementCreated = "RequirementCreated";
    private const string AuditEventRequirementUpdated = "RequirementUpdated";
    private const string AuditEventRequirementPlanCalculated = "RequirementPlanCalculated";
    private const string AuditEventIssueCreated = "IssueCreated";
    private const string AuditEventIssueCompleted = "IssueCompleted";
    private const string AuditEventStockChanged = "StockChanged";
    private const string AuditEventErrorPrevented = "ErrorPrevented";
    private const string VatRatePercentParameterKey = "MetalReceipt.VatRatePercent";
    private const decimal DefaultVatRatePercent = 22m;

    private readonly AppDbContext _dbContext;
    private readonly ICuttingMapExcelExporter _cuttingMapExcelExporter;
    private readonly ICuttingMapPdfExporter _cuttingMapPdfExporter;
    private readonly IMetalRequirementWarehousePrintDocumentService _requirementWarehousePrintDocumentService;
    private readonly IMetalReceiptItemLabelDocumentService _metalReceiptItemLabelDocumentService;
    private readonly IMetalReceiptDocumentService _metalReceiptDocumentService;
    private readonly ILogger<MetalWarehouseController> _logger;

    public MetalWarehouseController(
        AppDbContext dbContext,
        ICuttingMapExcelExporter cuttingMapExcelExporter,
        ICuttingMapPdfExporter cuttingMapPdfExporter,
        IMetalRequirementWarehousePrintDocumentService requirementWarehousePrintDocumentService,
        IMetalReceiptItemLabelDocumentService metalReceiptItemLabelDocumentService,
        IMetalReceiptDocumentService metalReceiptDocumentService,
        ILogger<MetalWarehouseController> logger)
    {
        _dbContext = dbContext;
        _cuttingMapExcelExporter = cuttingMapExcelExporter;
        _cuttingMapPdfExporter = cuttingMapPdfExporter;
        _requirementWarehousePrintDocumentService = requirementWarehousePrintDocumentService;
        _metalReceiptItemLabelDocumentService = metalReceiptItemLabelDocumentService;
        _metalReceiptDocumentService = metalReceiptDocumentService;
        _logger = logger;
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
        var receiptRows = await _dbContext.MetalReceipts
            .AsNoTracking()
            .OrderByDescending(x => x.ReceiptDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.ReceiptNumber,
                x.ReceiptDate,
                x.SupplierOrSource,
                x.SupplierIdentifierSnapshot,
                x.SupplierNameSnapshot,
                x.SupplierInnSnapshot,
                x.SupplierDocumentNumber,
                x.PricePerKg,
                x.AmountWithoutVat,
                x.VatAmount,
                x.TotalAmountWithVat,
                HasOriginalDocument = x.OriginalDocumentContent != null,
                Items = x.Items
                    .OrderBy(i => i.ReceiptLineIndex)
                    .ThenBy(i => i.ItemIndex)
                    .Select(i => new ReceiptItemSummaryProjection
                    {
                        ReceiptLineIndex = i.ReceiptLineIndex,
                        ItemIndex = i.ItemIndex,
                        MetalMaterialId = i.MetalMaterialId,
                        MaterialName = i.MetalMaterial != null ? i.MetalMaterial.Name : string.Empty,
                        Quantity = i.Quantity,
                        PassportWeightKg = i.PassportWeightKg,
                        SizeValue = i.SizeValue,
                        SizeUnitText = i.SizeUnitText,
                        IsSizeApproximate = i.IsSizeApproximate,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var model = new MetalReceiptListViewModel
        {
            Receipts = receiptRows.Select(x =>
            {
                var lines = BuildReceiptLineSummaries(x.Items);
                return new MetalReceiptListItemViewModel
                {
                    Id = x.Id,
                    ReceiptNumber = x.ReceiptNumber,
                    SupplierDisplay = BuildSupplierDisplay(x.SupplierIdentifierSnapshot, x.SupplierNameSnapshot, x.SupplierInnSnapshot, x.SupplierOrSource),
                    SupplierDocumentNumber = x.SupplierDocumentNumber,
                    MaterialsSummary = BuildMaterialsSummary(lines),
                    TotalQuantity = lines.Sum(line => line.Quantity),
                    TotalPassportWeightKg = lines.Sum(line => line.PassportWeightKg),
                    PricePerKg = x.PricePerKg,
                    AmountWithoutVat = x.AmountWithoutVat,
                    VatAmount = x.VatAmount,
                    TotalAmountWithVat = x.TotalAmountWithVat,
                    SizeSummary = BuildReceiptSizeSummary(lines),
                    HasOriginalDocument = x.HasOriginalDocument,
                };
            }).ToList(),
        };

        return View(model);
    }

    [HttpGet("Receipts/Create")]
    public async Task<IActionResult> CreateReceipt(CancellationToken cancellationToken)
    {
        await EnsureMetalMaterialsSeededAsync(cancellationToken);
        await EnsureMetalSuppliersSeededAsync(cancellationToken);
        await EnsureMetalReceiptParametersSeededAsync(cancellationToken);
        var model = new MetalReceiptCreateViewModel
        {
            ReceiptDate = DateTime.Today,
            AccountingAccount = "10.01",
            VatAccount = "19.03",
            VatRatePercent = await GetVatRatePercentAsync(cancellationToken),
            Items = new List<MetalReceiptLineInputViewModel>
            {
                new()
                {
                    Quantity = 1,
                    Units = new List<MetalReceiptUnitInputViewModel>
                    {
                        new() { ItemIndex = 1 },
                    },
                },
            },
        };

        await PopulateReceiptLookupsAsync(model, cancellationToken);
        return View("~/Views/MetalWarehouse/CreateReceipt.cshtml", model);
    }

    [HttpGet("Suppliers")]
    public async Task<IActionResult> Suppliers(CancellationToken cancellationToken)
    {
        await EnsureMetalSuppliersSeededAsync(cancellationToken);
        var model = new MetalSuppliersDirectoryViewModel
        {
            Suppliers = await GetSuppliersDirectoryItemsAsync(cancellationToken),
        };

        return View("~/Views/MetalWarehouse/Suppliers.cshtml", model);
    }

    [HttpPost("Suppliers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suppliers(MetalSuppliersDirectoryViewModel model, CancellationToken cancellationToken)
    {
        await EnsureMetalSuppliersSeededAsync(cancellationToken);

        var normalizedInn = Regex.Replace(model.Inn ?? string.Empty, @"\D", string.Empty);
        if (normalizedInn.Length is not (10 or 12))
        {
            ModelState.AddModelError(nameof(model.Inn), "ИНН должен содержать 10 или 12 цифр.");
        }

        var duplicateExists = await _dbContext.MetalSuppliers
            .AsNoTracking()
            .AnyAsync(x => x.IsActive
                && (x.Identifier == model.Identifier.Trim()
                    || x.Inn == normalizedInn
                    || x.Name == model.Name.Trim()), cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError(string.Empty, "Поставщик с таким кодом, ИНН или наименованием уже есть.");
        }

        if (!ModelState.IsValid)
        {
            model.Suppliers = await GetSuppliersDirectoryItemsAsync(cancellationToken);
            return View("~/Views/MetalWarehouse/Suppliers.cshtml", model);
        }

        _dbContext.MetalSuppliers.Add(new MetalSupplier
        {
            Id = Guid.NewGuid(),
            Identifier = model.Identifier.Trim(),
            Name = model.Name.Trim(),
            Inn = normalizedInn,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["MetalSuppliersSuccess"] = "Поставщик добавлен в справочник.";
        return RedirectToAction(nameof(Suppliers));
    }

    [HttpPost("Receipts/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReceipt(MetalReceiptCreateViewModel model, string submitAction, CancellationToken cancellationToken)
    {
        await EnsureMetalMaterialsSeededAsync(cancellationToken);
        await EnsureMetalSuppliersSeededAsync(cancellationToken);
        await EnsureMetalReceiptParametersSeededAsync(cancellationToken);
        NormalizeReceiptItems(model);
        var vatRatePercent = await GetVatRatePercentAsync(cancellationToken);
        RecalculateReceiptFinancials(model, vatRatePercent);


        var activeMaterials = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Name, x.Code })
            .ToListAsync(cancellationToken);

        foreach (var line in model.Items)
        {
            if (line.MetalMaterialId.HasValue || string.IsNullOrWhiteSpace(line.MaterialInputText))
            {
                continue;
            }

            var normalizedInput = NormalizeMaterialLookupText(line.MaterialInputText);
            var matches = activeMaterials
                .Where(x => NormalizeMaterialLookupText($"{x.Name} ({x.Code})") == normalizedInput
                    || NormalizeMaterialLookupText(x.Name) == normalizedInput
                    || NormalizeMaterialLookupText(x.Code) == normalizedInput)
                .ToList();

            if (matches.Count == 1)
            {
                line.MetalMaterialId = matches[0].Id;
            }
        }

        var activeSuppliers = await _dbContext.MetalSuppliers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Identifier, x.Name, x.Inn })
            .ToListAsync(cancellationToken);

        if (!model.SupplierId.HasValue && !string.IsNullOrWhiteSpace(model.SupplierInputText))
        {
            var normalizedSupplierInput = NormalizeSupplierLookupText(model.SupplierInputText);
            var matches = activeSuppliers
                .Where(x => NormalizeSupplierLookupText(BuildSupplierDisplay(x.Identifier, x.Name, x.Inn)) == normalizedSupplierInput
                    || NormalizeSupplierLookupText(x.Identifier) == normalizedSupplierInput
                    || NormalizeSupplierLookupText(x.Name) == normalizedSupplierInput
                    || NormalizeSupplierLookupText(x.Inn) == normalizedSupplierInput)
                .ToList();

            if (matches.Count == 1)
            {
                model.SupplierId = matches[0].Id;
            }
        }

        var hasActiveMaterials = await _dbContext.MetalMaterials
            .AsNoTracking()
            .AnyAsync(x => x.IsActive, cancellationToken);

        if (!hasActiveMaterials)
        {
            ModelState.AddModelError(string.Empty, "Справочник материалов пуст. Обратитесь к администратору.");
        }

        if (activeSuppliers.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Справочник поставщиков пуст. Добавьте поставщика перед приходом.");
        }

        var supplier = model.SupplierId.HasValue
            ? activeSuppliers.FirstOrDefault(x => x.Id == model.SupplierId.Value)
            : null;

        if (!model.SupplierId.HasValue)
        {
            ModelState.AddModelError(nameof(model.SupplierId), "Выберите поставщика из списка по ИНН или наименованию.");
        }
        else if (supplier is null)
        {
            ModelState.AddModelError(nameof(model.SupplierId), "Выбранный поставщик не найден или выключен.");
        }

        var materialIds = model.Items
            .Where(x => x.MetalMaterialId.HasValue)
            .Select(x => x.MetalMaterialId!.Value)
            .Distinct()
            .ToList();

        var materialsById = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        for (var lineIndex = 0; lineIndex < model.Items.Count; lineIndex++)
        {
            var line = model.Items[lineIndex];
            if (line.MetalMaterialId.HasValue && !materialsById.ContainsKey(line.MetalMaterialId.Value))
            {
                ModelState.AddModelError($"Items[{lineIndex}].MetalMaterialId", "Выбранный материал не найден или выключен.");
            }
        }

        var originalDocument = await ReadOriginalReceiptDocumentAsync(model.OriginalDocumentPdf, cancellationToken);
        if (!originalDocument.IsValid)
        {
            ModelState.AddModelError(nameof(model.OriginalDocumentPdf), originalDocument.ErrorMessage ?? "Не удалось прочитать DOCX.");
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Metal receipt create validation failed. Items: {Items}; Errors: {Errors}",
                model.Items.Select((x, i) => new
                {
                    Line = i + 1,
                    x.MetalMaterialId,
                    x.MaterialInputText,
                    x.Quantity,
                    x.PassportWeightKg,
                    x.PricePerKg,
                    UnitSizes = x.Units.Select(u => new { u.ItemIndex, u.SizeValue }).ToList(),
                }).ToList(),
                ModelState.Where(ms => ms.Value?.Errors.Count > 0)
                    .SelectMany(ms => ms.Value!.Errors.Select(e => $"{ms.Key}: {e.ErrorMessage}"))
                    .ToList());

            await PopulateReceiptLookupsAsync(model, cancellationToken);
            return View("~/Views/MetalWarehouse/CreateReceipt.cshtml", model);
        }

        var now = DateTime.UtcNow;
        var nextNumber = await GetNextReceiptNumberAsync(model.ReceiptDate, cancellationToken);
        var supplierDisplay = BuildSupplierDisplay(supplier!.Identifier, supplier.Name, supplier.Inn);
        var receipt = new MetalReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = nextNumber,
            ReceiptDate = ToUtcDate(model.ReceiptDate!.Value),
            MetalSupplierId = supplier.Id,
            SupplierOrSource = supplierDisplay,
            SupplierIdentifierSnapshot = supplier.Identifier,
            SupplierNameSnapshot = supplier.Name,
            SupplierInnSnapshot = supplier.Inn,
            SupplierDocumentNumber = model.SupplierDocumentNumber?.Trim(),
            InvoiceOrUpiNumber = model.InvoiceOrUpiNumber?.Trim(),
            AccountingAccount = "10.01",
            VatAccount = "19.03",
            PricePerKg = model.Items.FirstOrDefault()?.PricePerKg ?? model.PricePerKg ?? 0m,
            AmountWithoutVat = model.AmountWithoutVat,
            VatRatePercent = model.VatRatePercent,
            VatAmount = model.VatAmount,
            TotalAmountWithVat = model.TotalAmountWithVat,
            Comment = model.Comment?.Trim(),
            OriginalDocumentFileName = originalDocument.FileName,
            OriginalDocumentContentType = originalDocument.ContentType,
            OriginalDocumentContent = originalDocument.Content,
            OriginalDocumentSizeBytes = originalDocument.SizeBytes,
            OriginalDocumentUploadedAt = originalDocument.Content is null ? null : now,
            CreatedAt = now,
            BatchNumber = string.Empty,
        };

        var receiptItemIndex = 1;
        for (var lineIndex = 0; lineIndex < model.Items.Count; lineIndex++)
        {
            var line = model.Items[lineIndex];
            var material = materialsById[line.MetalMaterialId!.Value];
            var materialCode = BuildMaterialCode(material);
            var quantity = line.Quantity!.Value;
            var passportWeight = line.PassportWeightKg!.Value;
            var actualWeight = passportWeight;
            var calculatedWeight = CalculateWeightKg(line, material);
            var deviation = calculatedWeight - passportWeight;

            for (var unitIndex = 0; unitIndex < quantity; unitIndex++)
            {
                var unit = line.UseAverageSize ? null : line.Units[unitIndex];
                var inputSize = line.UseAverageSize ? line.AverageSizeValue : unit?.SizeValue;
                var suffix = receiptItemIndex.ToString("D3");
                var (sizeValue, sizeUnitText) = ResolveSizeFromInputOrMass(inputSize, material, quantity, actualWeight);
                var actualBlankSizeText = BuildActualBlankSizeText(sizeValue, sizeUnitText, line.UseAverageSize);
                var sizePart = sizeValue.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', '_');

                receipt.Items.Add(new MetalReceiptItem
                {
                    Id = Guid.NewGuid(),
                    MetalMaterialId = material.Id,
                    Quantity = quantity,
                    TotalWeightKg = actualWeight,
                    ReceiptLineIndex = lineIndex + 1,
                    ItemIndex = receiptItemIndex,
                    SizeValue = sizeValue,
                    SizeUnitText = sizeUnitText,
                    ActualBlankSizeText = actualBlankSizeText,
                    IsSizeApproximate = line.UseAverageSize,
                    PassportWeightKg = passportWeight,
                    PricePerKg = line.PricePerKg ?? model.PricePerKg ?? 0m,
                    ActualWeightKg = actualWeight,
                    CalculatedWeightKg = calculatedWeight,
                    WeightDeviationKg = deviation,
                    StockCategory = ResolveStockCategory(sizeUnitText, sizeValue),
                    GeneratedCode = $"{materialCode}-{sizePart}-{(sizeUnitText == "м2" ? "M2" : "M")}-{suffix}",
                    CreatedAt = now,
                });

                receiptItemIndex++;
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.MetalReceipts.Add(receipt);
        var nowUser = GetCurrentUserContext();
        foreach (var item in receipt.Items)
        {
            _dbContext.MetalStockMovements.Add(new MetalStockMovement
            {
                Id = Guid.NewGuid(),
                MovementDate = now,
                MovementType = "Receipt",
                MetalMaterialId = item.MetalMaterialId,
                MetalReceiptItemId = item.Id,
                SourceDocumentType = nameof(MetalReceipt),
                SourceDocumentId = receipt.Id,
                QtyBefore = 0m,
                QtyChange = item.SizeValue,
                QtyAfter = item.SizeValue,
                Unit = item.SizeUnitText,
                Comment = $"Приход по документу {receipt.ReceiptNumber}.",
                CreatedAt = now,
                CreatedBy = nowUser.UserName,
            });
        }

        AddAuditLog(
            AuditEventReceiptCreated,
            nameof(MetalReceipt),
            receipt.Id,
            receipt.ReceiptNumber,
            "Создан приход металла.",
            new
            {
                receipt.ReceiptDate,
                Supplier = supplierDisplay,
                receipt.SupplierDocumentNumber,
                receipt.PricePerKg,
                receipt.AmountWithoutVat,
                receipt.VatRatePercent,
                receipt.VatAmount,
                receipt.TotalAmountWithVat,
                receipt.Comment,
                Lines = model.Items.Count,
                Units = receipt.Items.Count,
                HasOriginalDocument = receipt.OriginalDocumentContent is not null,
            });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["MetalReceiptSuccess"] = "Приход металла успешно создан";
        TempData["MetalReceiptId"] = receipt.Id;
        TempData["MetalReceiptNumber"] = receipt.ReceiptNumber;

        if (string.Equals(submitAction, "saveAddNext", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(CreateReceipt));
        }

        return RedirectToAction(nameof(ReceiptDocument), new { id = receipt.Id });
    }

    [HttpPost("Receipts/AddSupplierInline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSupplierInline([FromBody] MetalSupplierInlineCreateModel model, CancellationToken cancellationToken)
    {
        var inn = Regex.Replace(model.Inn ?? string.Empty, @"\D", string.Empty);
        if (string.IsNullOrWhiteSpace(model.Identifier) || string.IsNullOrWhiteSpace(model.Name) || inn.Length is not (10 or 12))
        {
            return BadRequest(new { message = "Заполните код, наименование и корректный ИНН (10/12 цифр)." });
        }

        var supplier = new MetalSupplier
        {
            Id = Guid.NewGuid(),
            Identifier = model.Identifier.Trim(),
            Name = model.Name.Trim(),
            Inn = inn,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.MetalSuppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Json(new { id = supplier.Id, text = BuildSupplierDisplay(supplier.Identifier, supplier.Name, supplier.Inn) });
    }

    [HttpPost("Receipts/AddMaterialInline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMaterialInline([FromBody] MetalMaterialInlineCreateModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return BadRequest(new { message = "Укажите наименование материала." });
        }

        var unitKind = model.UnitKind == "SquareMeter" ? "SquareMeter" : "Meter";
        var weight = model.WeightPerUnitKg > 0m ? model.WeightPerUnitKg : 1m;
        var material = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Code = (model.Code ?? string.Empty).Trim(),
            UnitKind = unitKind,
            MassPerMeterKg = unitKind == "Meter" ? weight : 0m,
            MassPerSquareMeterKg = unitKind == "SquareMeter" ? weight : 0m,
            CoefConsumption = 1m,
            StockUnit = unitKind == "SquareMeter" ? "m2" : "m",
            WeightPerUnitKg = weight,
            Coefficient = 1m,
            IsActive = true,
        };
        _dbContext.MetalMaterials.Add(material);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Json(new { id = material.Id, text = string.IsNullOrWhiteSpace(material.Code) ? material.Name : $"{material.Name} ({material.Code})", unitKind = material.UnitKind, weightPerUnitKg = weight, coefficient = 1m });
    }

    [HttpPost("Receipts/Create")]
    [ValidateAntiForgeryToken]
    private async Task<IActionResult> CreateReceiptLegacy(MetalReceiptCreateViewModel model, string submitAction, CancellationToken cancellationToken)
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


        var activeMaterials = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Name, x.Code })
            .ToListAsync(cancellationToken);

        foreach (var line in model.Items)
        {
            if (line.MetalMaterialId.HasValue || string.IsNullOrWhiteSpace(line.MaterialInputText))
            {
                continue;
            }

            var normalizedInput = NormalizeMaterialLookupText(line.MaterialInputText);
            var matches = activeMaterials
                .Where(x => NormalizeMaterialLookupText($"{x.Name} ({x.Code})") == normalizedInput
                    || NormalizeMaterialLookupText(x.Name) == normalizedInput
                    || NormalizeMaterialLookupText(x.Code) == normalizedInput)
                .ToList();

            if (matches.Count == 1)
            {
                line.MetalMaterialId = matches[0].Id;
            }
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
            _logger.LogWarning("Metal receipt create validation failed. Items: {Items}; Errors: {Errors}",
                model.Items.Select((x, i) => new
                {
                    Line = i + 1,
                    x.MetalMaterialId,
                    x.MaterialInputText,
                    x.Quantity,
                    x.PassportWeightKg,
                    x.PricePerKg,
                    UnitSizes = x.Units.Select(u => new { u.ItemIndex, u.SizeValue }).ToList(),
                }).ToList(),
                ModelState.Where(ms => ms.Value?.Errors.Count > 0)
                    .SelectMany(ms => ms.Value!.Errors.Select(e => $"{ms.Key}: {e.ErrorMessage}"))
                    .ToList());

            await PopulateReceiptLookupsAsync(model, cancellationToken);
            return View("~/Views/MetalWarehouse/CreateReceipt.cshtml", model);
        }

        ArgumentNullException.ThrowIfNull(material);

        var now = DateTime.UtcNow;
        var nextNumber = await GetNextReceiptNumberAsync(model, material, cancellationToken);
        var receipt = new MetalReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = nextNumber,
            ReceiptDate = ToUtcDate(model.ReceiptDate!.Value),
            Comment = model.Comment?.Trim(),
            CreatedAt = now,
        };

        var materialCode = BuildMaterialCode(material);
        var quantity = model.Quantity!.Value;
        var passportWeight = model.PassportWeightKg!.Value;
        var actualWeight = passportWeight;
        var calculatedWeight = CalculateWeightKg(model, material);
        var deviation = actualWeight - passportWeight;

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Metal receipt create validation failed. Items: {Items}; Errors: {Errors}",
                model.Items.Select((x, i) => new
                {
                    Line = i + 1,
                    x.MetalMaterialId,
                    x.MaterialInputText,
                    x.Quantity,
                    x.PassportWeightKg,
                    x.PricePerKg,
                    UnitSizes = x.Units.Select(u => new { u.ItemIndex, u.SizeValue }).ToList(),
                }).ToList(),
                ModelState.Where(ms => ms.Value?.Errors.Count > 0)
                    .SelectMany(ms => ms.Value!.Errors.Select(e => $"{ms.Key}: {e.ErrorMessage}"))
                    .ToList());

            await PopulateReceiptLookupsAsync(model, cancellationToken);
            return View("~/Views/MetalWarehouse/CreateReceipt.cshtml", model);
        }

        receipt.BatchNumber = string.Empty;
        for (var i = 0; i < quantity; i++)
        {
            var unit = model.Units[i];
            var suffix = (i + 1).ToString("D3");
            var (sizeValue, sizeUnitText) = ResolveSizeFromInputOrMass(unit.SizeValue, material, quantity, actualWeight);
            var actualBlankSizeText = BuildActualBlankSizeText(sizeValue, sizeUnitText);
            var sizePart = sizeValue.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', '_');
            receipt.Items.Add(new MetalReceiptItem
            {
                Id = Guid.NewGuid(),
                MetalMaterialId = material.Id,
                Quantity = quantity,
                TotalWeightKg = actualWeight,
                ItemIndex = i + 1,
                SizeValue = sizeValue,
                SizeUnitText = sizeUnitText,
                ActualBlankSizeText = actualBlankSizeText,
                PassportWeightKg = passportWeight,
                    PricePerKg = model.PricePerKg ?? 0m,
                ActualWeightKg = actualWeight,
                CalculatedWeightKg = calculatedWeight,
                WeightDeviationKg = deviation,
                StockCategory = ResolveStockCategory(sizeUnitText, sizeValue),
                GeneratedCode = $"{materialCode}-{sizePart}-{(sizeUnitText == "м2" ? "M2" : "M")}-{suffix}",
                CreatedAt = now,
            });
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.MetalReceipts.Add(receipt);
        var nowUser = GetCurrentUserContext();
        foreach (var item in receipt.Items)
        {
            _dbContext.MetalStockMovements.Add(new MetalStockMovement
            {
                Id = Guid.NewGuid(),
                MovementDate = now,
                MovementType = "Receipt",
                MetalMaterialId = item.MetalMaterialId,
                MetalReceiptItemId = item.Id,
                SourceDocumentType = nameof(MetalReceipt),
                SourceDocumentId = receipt.Id,
                QtyBefore = 0m,
                QtyChange = item.SizeValue,
                QtyAfter = item.SizeValue,
                Unit = item.SizeUnitText,
                Comment = $"Приход по документу {receipt.ReceiptNumber}.",
                CreatedAt = now,
                CreatedBy = nowUser.UserName,
            });
        }

        AddAuditLog(
            AuditEventReceiptCreated,
            nameof(MetalReceipt),
            receipt.Id,
            receipt.ReceiptNumber,
            "Создан приход металла.",
            new { receipt.ReceiptDate, receipt.Comment, Units = receipt.Items.Count });
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["MetalReceiptSuccess"] = "Приход металла успешно создан";
        TempData["MetalReceiptId"] = receipt.Id;
        TempData["MetalReceiptNumber"] = receipt.ReceiptNumber;

        if (string.Equals(submitAction, "saveAddNext", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(CreateReceipt));
        }

        return RedirectToAction(nameof(ReceiptDocument), new { id = receipt.Id });
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
                x.SupplierIdentifierSnapshot,
                x.SupplierNameSnapshot,
                x.SupplierInnSnapshot,
                x.SupplierDocumentNumber,
                x.PricePerKg,
                x.AmountWithoutVat,
                x.VatRatePercent,
                x.VatAmount,
                x.TotalAmountWithVat,
                x.Comment,
                x.OriginalDocumentFileName,
                HasOriginalDocument = x.OriginalDocumentContent != null,
                Items = x.Items
                    .OrderBy(i => i.ReceiptLineIndex)
                    .ThenBy(i => i.ItemIndex)
                    .Select(i => new ReceiptItemDetailsProjection
                    {
                        Id = i.Id,
                        ReceiptLineIndex = i.ReceiptLineIndex,
                        ItemIndex = i.ItemIndex,
                        SizeValue = i.SizeValue,
                        SizeUnitText = i.SizeUnitText,
                        ActualBlankSizeText = i.ActualBlankSizeText,
                        IsSizeApproximate = i.IsSizeApproximate,
                        GeneratedCode = i.GeneratedCode,
                        PassportWeightKg = i.PassportWeightKg,
                        ActualWeightKg = i.ActualWeightKg,
                        CalculatedWeightKg = i.CalculatedWeightKg,
                        WeightDeviationKg = i.WeightDeviationKg,
                        Quantity = i.Quantity,
                        MetalMaterialId = i.MetalMaterialId,
                        MaterialName = i.MetalMaterial != null ? i.MetalMaterial.Name : string.Empty,
                        MaterialCoefficient = i.MetalMaterial != null ? i.MetalMaterial.Coefficient : 0m,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (receipt is null || receipt.Items.Count == 0)
        {
            return NotFound();
        }

        var lines = BuildReceiptLineDetails(receipt.Items);
        var first = lines[0];
        var model = new MetalReceiptDetailsViewModel
        {
            Id = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate,
            SupplierDisplay = BuildSupplierDisplay(receipt.SupplierIdentifierSnapshot, receipt.SupplierNameSnapshot, receipt.SupplierInnSnapshot, receipt.SupplierOrSource),
            SupplierDocumentNumber = receipt.SupplierDocumentNumber,
            Comment = receipt.Comment,
            MaterialName = BuildMaterialsSummary(lines),
            PassportWeightKg = lines.Sum(x => x.PassportWeightKg),
            CalculatedWeightKg = lines.Sum(x => x.CalculatedWeightKg),
            WeightDeviationKg = lines.Sum(x => x.WeightDeviationKg),
            CalculatedWeightFormula = first.CalculatedWeightFormula,
            WeightDeviationFormula = first.WeightDeviationFormula,
            Quantity = lines.Sum(x => x.Quantity),
            PricePerKg = receipt.PricePerKg,
            AmountWithoutVat = receipt.AmountWithoutVat,
            VatRatePercent = receipt.VatRatePercent,
            VatAmount = receipt.VatAmount,
            TotalAmountWithVat = receipt.TotalAmountWithVat,
            HasOriginalDocument = receipt.HasOriginalDocument,
            OriginalDocumentFileName = receipt.OriginalDocumentFileName,
            Lines = lines,
            Items = receipt.Items
                .Select(i => new MetalReceiptDetailsItemViewModel
                {
                    Id = i.Id,
                    ReceiptLineIndex = i.ReceiptLineIndex,
                    ItemIndex = i.ItemIndex,
                    MaterialName = i.MaterialName,
                    SizeValue = i.SizeValue,
                    SizeUnitText = i.SizeUnitText,
                    ActualBlankSizeText = i.ActualBlankSizeText,
                    IsSizeApproximate = i.IsSizeApproximate,
                    GeneratedCode = i.GeneratedCode,
                })
                .ToList(),
        };

        return View("~/Views/MetalWarehouse/ReceiptDetails.cshtml", model);
    }

    [HttpGet("Receipts/Details/{id:guid}")]
    private async Task<IActionResult> ReceiptDetailsLegacy(Guid id, CancellationToken cancellationToken)
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
                        i.Id,
                        i.ItemIndex,
                        i.SizeValue,
                        i.SizeUnitText,
                        i.ActualBlankSizeText,
                        i.GeneratedCode,
                        i.PassportWeightKg,
                        i.ActualWeightKg,
                        i.CalculatedWeightKg,
                        i.WeightDeviationKg,
                        MaterialCoefficient = i.MetalMaterial != null ? i.MetalMaterial.Coefficient : 0m,
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
            CalculatedWeightFormula = BuildCalculatedWeightFormula(first.PassportWeightKg, first.MaterialCoefficient, first.CalculatedWeightKg),
            WeightDeviationFormula = BuildWeightDeviationFormula(first.ActualWeightKg, first.PassportWeightKg, first.WeightDeviationKg),
            Quantity = (int)first.Quantity,
            Items = receipt.Item
                .Select(i => new MetalReceiptDetailsItemViewModel
                {
                    Id = i.Id,
                    ItemIndex = i.ItemIndex,
                    SizeValue = i.SizeValue,
                    SizeUnitText = i.SizeUnitText,
                    ActualBlankSizeText = i.ActualBlankSizeText,
                    GeneratedCode = i.GeneratedCode,
                })
                .ToList(),
        };

        return View("~/Views/MetalWarehouse/ReceiptDetails.cshtml", model);
    }

    [HttpGet("Stock")]
    public async Task<IActionResult> Stock([FromQuery] MetalStockFilterViewModel filter, CancellationToken cancellationToken)
    {
        var showConsumed = filter.ShowConsumed;
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
                HasUsage = x.StockMovements.Any(m => m.QtyChange < 0m),
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

        if (!showConsumed)
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
                ShowConsumed = showConsumed,
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
                Status = ResolveStockStatus(x.IsConsumed, x.HasUsage, x.SizeValue),
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
                x.SourceDocumentType,
                x.SourceDocumentId,
                x.CreatedBy,
            })
            .ToListAsync(cancellationToken);

        var history = new List<MetalStockItemHistoryEntryViewModel>
        {
            new()
            {
                Timestamp = item.CreatedAt,
                EventName = "Приход",
                Description = $"Единица добавлена по документу {item.ReceiptNumber}.",
                DocumentNumber = item.ReceiptNumber,
                UserName = "system",
            },
        };

        history.AddRange(movements.Select(x => new MetalStockItemHistoryEntryViewModel
        {
            Timestamp = x.MovementDate,
            EventName = x.MovementType switch
            {
                "Receipt" => "Приход",
                MovementTypeIssue => "Выдача по требованию",
                MovementTypeResidualUpdate => "Изменение остатка",
                MovementTypeFullConsumption => "Полный расход",
                _ => "Движение",
            },
            Description = $"{(x.QtyBefore ?? 0m):0.###} -> {(x.QtyAfter ?? 0m):0.###} {x.Unit} (изменение {x.QtyChange:0.###}). {(string.IsNullOrWhiteSpace(x.Comment) ? string.Empty : x.Comment)}".Trim(),
            SourceDocumentType = x.SourceDocumentType,
            SourceDocumentId = x.SourceDocumentId,
            DocumentNumber = x.Comment,
            UserName = x.CreatedBy,
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

    [HttpGet("ReceiptItems/{id:guid}/Label")]
    public async Task<IActionResult> ReceiptItemLabel(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор единицы прихода.");
        }

        try
        {
            var document = await _metalReceiptItemLabelDocumentService.BuildAsync(id, cancellationToken);
            return File(document.Content, document.ContentType, document.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("Receipts/{id:guid}/Document")]
    public async Task<IActionResult> ReceiptDocument(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _metalReceiptDocumentService.BuildAsync(id, cancellationToken);
            return File(document.Content, document.ContentType, document.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("Receipts/{id:guid}/DocumentPdf")]
    public async Task<IActionResult> ReceiptDocumentPdf(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _metalReceiptDocumentService.BuildPdfAsync(id, cancellationToken);
<<<<<<< codex/fix-line-number-in-receipt-document-ihws17
=======
            Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";
>>>>>>> master
            return File(document.Content, document.ContentType);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("Receipts/{id:guid}/OriginalDocument")]
    public async Task<IActionResult> ReceiptOriginalDocument(Guid id, CancellationToken cancellationToken)
    {
        var document = await _dbContext.MetalReceipts
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.OriginalDocumentFileName,
                x.OriginalDocumentContentType,
                x.OriginalDocumentContent,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (document?.OriginalDocumentContent is null || document.OriginalDocumentContent.Length == 0)
        {
            return NotFound();
        }

        var fileName = string.IsNullOrWhiteSpace(document.OriginalDocumentFileName)
            ? $"Оригинал_{id:N}.docx"
            : document.OriginalDocumentFileName;
        var contentType = string.IsNullOrWhiteSpace(document.OriginalDocumentContentType)
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : document.OriginalDocumentContentType;

        return File(document.OriginalDocumentContent, contentType, fileName);
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
        await EnsureRequirementPlanCalculatedAsync(id, cancellationToken);
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
            AddAuditLog(AuditEventErrorPrevented, nameof(MetalRequirement), requirement.Id, requirement.RequirementNumber, "Попытка повторно оформить выдачу по исполненному требованию заблокирована.");
            TempData["MetalRequirementError"] = "Требование уже исполнено. Повторное оформление выдачи запрещено.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }

        if (requirement.Status == RequirementCancelledStatus)
        {
            AddAuditLog(AuditEventErrorPrevented, nameof(MetalRequirement), requirement.Id, requirement.RequirementNumber, "Попытка оформить выдачу по отмененному требованию заблокирована.");
            TempData["MetalRequirementError"] = "Требование отменено. Оформление выдачи запрещено.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }

        var existingIssue = await _dbContext.MetalIssues
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MetalRequirementId == id && x.Status != IssueCancelledStatus, cancellationToken);
        if (existingIssue is not null)
        {
            AddAuditLog(AuditEventErrorPrevented, nameof(MetalRequirement), requirement.Id, requirement.RequirementNumber, "Попытка создать вторую выдачу по требованию заблокирована.");
            return RedirectToAction(nameof(IssueDetails), new { id = existingIssue.Id });
        }

        var plan = await _dbContext.MetalRequirementPlans
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.MetalRequirementId == id, cancellationToken);
        if (plan is null || plan.Items.Count == 0)
        {
            AddAuditLog(AuditEventErrorPrevented, nameof(MetalRequirement), requirement.Id, requirement.RequirementNumber, "Попытка создать выдачу без рассчитанного плана заблокирована.");
            TempData["MetalRequirementError"] = "Невозможно оформить выдачу: план подбора не рассчитан.";
            return RedirectToAction(nameof(RequirementDetails), new { id });
        }

        if (plan.DeficitQty > 0m)
        {
            var deficitUnit = requirement.Items.FirstOrDefault()?.Unit ?? "мм";
            AddAuditLog(AuditEventErrorPrevented, nameof(MetalRequirement), requirement.Id, requirement.RequirementNumber, $"Попытка создать выдачу при дефиците {plan.DeficitQty:0.###} {deficitUnit} заблокирована.");
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
        requirement.Status = RequirementReadyToIssueStatus;
        requirement.UpdatedAt = DateTime.UtcNow;
        requirement.UpdatedBy = User?.Identity?.Name ?? "system";
        AddAuditLog(AuditEventIssueCreated, nameof(MetalIssue), issue.Id, issue.IssueNumber, $"Создана электронная выдача по требованию {requirement.RequirementNumber}.", new { requirementId = requirement.Id });
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

        var history = await _dbContext.MetalAuditLogs
            .AsNoTracking()
            .Where(x => (x.EntityType == nameof(MetalIssue) && x.EntityId == issue.Id) || (x.EntityType == nameof(MetalRequirement) && x.EntityId == issue.MetalRequirementId))
            .OrderByDescending(x => x.EventDate)
            .Select(x => new MetalAuditLogEntryViewModel
            {
                EventDate = x.EventDate,
                EventType = x.EventType,
                UserName = string.IsNullOrWhiteSpace(x.UserName) ? "system" : x.UserName!,
                Message = x.Message,
            })
            .ToListAsync(cancellationToken);

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
            History = history,
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
            AddAuditLog(AuditEventErrorPrevented, nameof(MetalIssue), issue.Id, issue.IssueNumber, "Попытка повторно провести Completed выдачу заблокирована.");
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
            AddAuditLog(AuditEventErrorPrevented, nameof(MetalRequirement), requirement.Id, requirement.RequirementNumber, "Попытка провести выдачу по уже исполненному требованию заблокирована.");
            TempData["MetalIssueError"] = "Требование уже исполнено другой выдачей. Повторное проведение запрещено.";
            return RedirectToAction(nameof(IssueDetails), new { id });
        }

        if (requirement.Status == RequirementCancelledStatus)
        {
            AddAuditLog(AuditEventErrorPrevented, nameof(MetalRequirement), requirement.Id, requirement.RequirementNumber, "Попытка провести выдачу по отмененному требованию заблокирована.");
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
                AddAuditLog(AuditEventErrorPrevented, nameof(MetalIssue), issue.Id, issue.IssueNumber, $"Проведение выдачи заблокировано: не найдена исходная единица {line.SourceCode}.");
                await tx.RollbackAsync(cancellationToken);
                TempData["MetalIssueError"] = $"Не найдена исходная единица {line.SourceCode}.";
                return RedirectToAction(nameof(IssueDetails), new { id });
            }

            if (source.IsConsumed)
            {
                AddAuditLog(AuditEventErrorPrevented, nameof(MetalIssue), issue.Id, issue.IssueNumber, $"Проведение выдачи заблокировано: единица {source.GeneratedCode} уже недоступна.");
                await tx.RollbackAsync(cancellationToken);
                TempData["MetalIssueError"] = $"Единица {source.GeneratedCode} уже недоступна для выдачи.";
                return RedirectToAction(nameof(IssueDetails), new { id });
            }

            if (source.SizeValue < line.IssuedQty)
            {
                AddAuditLog(AuditEventErrorPrevented, nameof(MetalIssue), issue.Id, issue.IssueNumber, $"Проведение выдачи заблокировано: недостаточный остаток по {source.GeneratedCode}.");
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
            AddAuditLog(AuditEventStockChanged, nameof(MetalReceiptItem), source.Id, source.GeneratedCode, $"Изменение остатка по выдаче {issue.IssueNumber}: {(qtyBefore):0.###} -> {line.RemainingQtyAfter:0.###} {source.SizeUnitText}.", new { issueId = issue.Id, requirementId = requirement.Id, line.IssuedQty });
        }

        issue.Status = IssueCompletedStatus;
        issue.CompletedAt = now;
        issue.CompletedBy = userName;
        requirement.Status = IssueCompletedStatus;
        requirement.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        AddAuditLog(AuditEventIssueCompleted, nameof(MetalIssue), issue.Id, issue.IssueNumber, $"Выдача {issue.IssueNumber} подтверждена.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(IssueDetails), new { id });
    }

    [HttpPost("Requirements/Details/{id:guid}/plan/calculate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CalculateRequirementPlan(Guid id, CancellationToken cancellationToken)
    {
        TempData["MetalRequirementError"] = "Ручной пересчет отключен. План рассчитывается автоматически.";
        return RedirectToAction(nameof(RequirementDetails), new { id });
    }

    private async Task EnsureRequirementPlanCalculatedAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var hasPlan = await _dbContext.MetalRequirementPlans.AsNoTracking().AnyAsync(x => x.MetalRequirementId == requirementId, cancellationToken);
        if (hasPlan) return;
        await BuildAndSaveRequirementPlanAsync(requirementId, cancellationToken);
    }

    private async Task BuildAndSaveRequirementPlanAsync(Guid id, CancellationToken cancellationToken)
    {
        var requirement = await _dbContext.MetalRequirements.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (requirement is null || requirement.Status == IssueCompletedStatus || requirement.Status == RequirementCancelledStatus) return;
        var existingPlan = await _dbContext.MetalRequirementPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.MetalRequirementId == id, cancellationToken);
        var now = DateTime.UtcNow; var userName = User?.Identity?.Name ?? "system"; var unit = requirement.Items.FirstOrDefault()?.Unit ?? "мм";
        var baseRequiredQty = requirement.Items.Sum(x => x.RequiredQty); var adjustedRequiredQty = requirement.Items.Sum(x => x.TotalRequiredQty > 0m ? x.TotalRequiredQty : x.RequiredQty); var requiredQty = adjustedRequiredQty;
        var receiptItems = await _dbContext.MetalReceiptItems.AsNoTracking().Where(x => requirement.Items.Select(i => i.MetalMaterialId).Contains(x.MetalMaterialId) && !x.IsConsumed && x.SizeValue > 0m).Select(x => new { x.Id, x.GeneratedCode, x.SizeValue, x.SizeUnitText, UnitWeightKg = x.Quantity > 0 ? x.TotalWeightKg / x.Quantity : x.TotalWeightKg, ReceiptDate = x.MetalReceipt != null ? x.MetalReceipt.ReceiptDate : x.CreatedAt }).ToListAsync(cancellationToken);
        var sortedSources = receiptItems.OrderBy(x => Math.Abs(x.SizeValue - requiredQty)).ThenBy(x => x.ReceiptDate).ToList();
        var remainingRequired = requiredQty; var planItems = new List<MetalRequirementPlanItem>(); var order = 1;
        foreach (var source in sortedSources){ if (remainingRequired <= 0m){ planItems.Add(new MetalRequirementPlanItem{ Id = Guid.NewGuid(), MetalReceiptItemId = source.Id, SourceCode = source.GeneratedCode, SourceSize = source.SizeValue, SourceUnit = source.SizeUnitText, SourceWeightKg = source.UnitWeightKg, PlannedUseQty = 0m, RemainingAfterQty = source.SizeValue, LineStatus = LineStatusReserveCandidate, SortOrder = order++, }); continue; } var plannedUse = Math.Min(source.SizeValue, remainingRequired); var remainingAfter = source.SizeValue - plannedUse; planItems.Add(new MetalRequirementPlanItem{ Id = Guid.NewGuid(), MetalReceiptItemId = source.Id, SourceCode = source.GeneratedCode, SourceSize = source.SizeValue, SourceUnit = source.SizeUnitText, SourceWeightKg = source.UnitWeightKg, PlannedUseQty = plannedUse, RemainingAfterQty = remainingAfter, LineStatus = remainingAfter > 0m ? LineStatusPartialCut : LineStatusFullUse, SortOrder = order++, }); remainingRequired -= plannedUse; }
        if (remainingRequired > 0m){ planItems.Add(new MetalRequirementPlanItem{ Id = Guid.NewGuid(), MetalReceiptItemId = null, SourceCode = "Дефицит", SourceSize = 0m, SourceUnit = unit, PlannedUseQty = remainingRequired, RemainingAfterQty = 0m, LineStatus = LineStatusDeficit, SortOrder = order, }); }
        var plannedQty = planItems.Where(x => x.LineStatus != LineStatusReserveCandidate && x.LineStatus != LineStatusDeficit).Sum(x => x.PlannedUseQty); var deficitQty = Math.Max(0m, requiredQty - plannedQty);
        if (existingPlan is null){ existingPlan = new MetalRequirementPlan{ Id = Guid.NewGuid(), MetalRequirementId = id, CreatedAt = now, CreatedBy = userName, }; _dbContext.MetalRequirementPlans.Add(existingPlan);} else { _dbContext.MetalRequirementPlanItems.RemoveRange(existingPlan.Items); existingPlan.RecalculatedAt = now; existingPlan.RecalculatedBy = userName; }
        existingPlan.Status = deficitQty > 0m ? PlanHasDeficitStatus : PlanCalculatedStatus; existingPlan.BaseRequiredQty = baseRequiredQty; existingPlan.AdjustedRequiredQty = adjustedRequiredQty; existingPlan.PlannedQty = plannedQty; existingPlan.DeficitQty = deficitQty; existingPlan.CalculationComment = deficitQty > 0m ? $"Не хватает {deficitQty:0.###} {unit}." : "План рассчитан без дефицита."; existingPlan.Items = planItems;
        requirement.Status = deficitQty > 0m ? RequirementStatusUpdated : RequirementPlannedStatus; requirement.UpdatedAt = now; requirement.UpdatedBy = userName;
        AddAuditLog(AuditEventRequirementPlanCalculated, nameof(MetalRequirementPlan), existingPlan.Id, requirement.RequirementNumber, deficitQty > 0m ? $"План подбора рассчитан с дефицитом {deficitQty:0.###} {unit}." : "План подбора рассчитан без дефицита.", new { requirementId = requirement.Id, plannedQty, deficitQty });
        await _dbContext.SaveChangesAsync(cancellationToken);
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
    public async Task<IActionResult> Movements([FromQuery] MetalMovementsFilterViewModel filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.MetalStockMovements
            .AsNoTracking()
            .Select(x => new
            {
                x.MovementDate,
                x.MovementType,
                Material = x.MetalMaterial != null ? x.MetalMaterial.Name : "—",
                MetalUnitCode = x.MetalReceiptItem != null ? x.MetalReceiptItem.GeneratedCode : "—",
                x.SourceDocumentType,
                x.QtyBefore,
                x.QtyChange,
                x.QtyAfter,
                x.Unit,
                x.CreatedBy,
                x.Comment,
                x.MetalMaterialId,
            });

        if (filter.DateFrom.HasValue)
        {
            query = query.Where(x => x.MovementDate >= filter.DateFrom.Value.Date);
        }

        if (filter.DateTo.HasValue)
        {
            var toExclusive = filter.DateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.MovementDate < toExclusive);
        }

        if (filter.MaterialId.HasValue)
        {
            query = query.Where(x => x.MetalMaterialId == filter.MaterialId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.MovementType))
        {
            query = query.Where(x => x.MovementType == filter.MovementType);
        }

        if (!string.IsNullOrWhiteSpace(filter.DocumentNumber))
        {
            query = query.Where(x => (x.Comment ?? string.Empty).Contains(filter.DocumentNumber.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(filter.MetalUnitCode))
        {
            query = query.Where(x => x.MetalUnitCode.Contains(filter.MetalUnitCode.Trim()));
        }

        var rows = await query
            .OrderByDescending(x => x.MovementDate)
            .Take(500)
            .ToListAsync(cancellationToken);

        var model = new MetalMovementsPageViewModel
        {
            Filter = new MetalMovementsFilterViewModel
            {
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo,
                MaterialId = filter.MaterialId,
                MovementType = filter.MovementType,
                DocumentNumber = filter.DocumentNumber,
                MetalUnitCode = filter.MetalUnitCode,
                Materials = await _dbContext.MetalMaterials.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(x.Code) ? x.Name : $"{x.Name} ({x.Code})",
                    Selected = filter.MaterialId == x.Id,
                }).ToListAsync(cancellationToken),
                MovementTypes = await _dbContext.MetalStockMovements.AsNoTracking().Select(x => x.MovementType).Distinct().OrderBy(x => x).Select(x => new SelectListItem
                {
                    Value = x,
                    Text = x,
                    Selected = filter.MovementType == x,
                }).ToListAsync(cancellationToken),
            },
            Rows = rows.Select(x => new MetalMovementRowViewModel
            {
                Date = x.MovementDate,
                MovementType = x.MovementType,
                Material = x.Material,
                MetalUnitCode = x.MetalUnitCode,
                Document = x.Comment ?? x.SourceDocumentType,
                Before = x.QtyBefore,
                Change = x.QtyChange,
                After = x.QtyAfter,
                Unit = x.Unit,
                User = x.CreatedBy,
            }).ToList(),
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

        var materialIds = requirement.Items
            .Select(i => i.MetalMaterialId)
            .Where(x => x.HasValue && x.Value != Guid.Empty)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
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

        var history = await _dbContext.MetalAuditLogs
            .AsNoTracking()
            .Where(x => (x.EntityType == nameof(MetalRequirement) && x.EntityId == requirement.Id)
                        || (x.EntityType == nameof(MetalRequirementPlan) && requirementPlan != null && x.EntityId == requirementPlan.Id)
                        || (existingIssue != null && x.EntityType == nameof(MetalIssue) && x.EntityId == existingIssue.Id))
            .OrderByDescending(x => x.EventDate)
            .Select(x => new MetalAuditLogEntryViewModel
            {
                EventDate = x.EventDate,
                EventType = x.EventType,
                UserName = string.IsNullOrWhiteSpace(x.UserName) ? "system" : x.UserName!,
                Message = x.Message,
            })
            .ToListAsync(cancellationToken);

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
            History = history,
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

    private static IReadOnlyList<ReceiptLineSummary> BuildReceiptLineSummaries(IEnumerable<ReceiptItemSummaryProjection> items)
    {
        return items
            .GroupBy(x => new
            {
                LineIndex = x.ReceiptLineIndex <= 0 ? x.ItemIndex : x.ReceiptLineIndex,
                x.MetalMaterialId,
                x.MaterialName,
            })
            .OrderBy(x => x.Key.LineIndex)
            .Select(group =>
            {
                var lineItems = group.OrderBy(x => x.ItemIndex).ToList();
                var first = lineItems[0];
                var quantity = first.Quantity > 0m ? (int)first.Quantity : lineItems.Count;
                return new ReceiptLineSummary(
                    group.Key.LineIndex,
                    string.IsNullOrWhiteSpace(group.Key.MaterialName) ? "-" : group.Key.MaterialName,
                    quantity,
                    first.PassportWeightKg,
                    lineItems.Sum(x => x.SizeValue),
                    first.SizeUnitText,
                    BuildLineSizeSummary(lineItems),
                    lineItems.Any(x => x.IsSizeApproximate));
            })
            .ToList();
    }

    private static IReadOnlyList<MetalReceiptDetailsLineViewModel> BuildReceiptLineDetails(IEnumerable<ReceiptItemDetailsProjection> items)
    {
        return items
            .GroupBy(x => new
            {
                LineIndex = x.ReceiptLineIndex <= 0 ? x.ItemIndex : x.ReceiptLineIndex,
                x.MetalMaterialId,
                x.MaterialName,
            })
            .OrderBy(x => x.Key.LineIndex)
            .Select(group =>
            {
                var lineItems = group.OrderBy(x => x.ItemIndex).ToList();
                var first = lineItems[0];
                var quantity = first.Quantity > 0m ? (int)first.Quantity : lineItems.Count;
                return new MetalReceiptDetailsLineViewModel
                {
                    LineIndex = group.Key.LineIndex,
                    MaterialName = string.IsNullOrWhiteSpace(group.Key.MaterialName) ? "-" : group.Key.MaterialName,
                    Quantity = quantity,
                    PassportWeightKg = first.PassportWeightKg,
                    CalculatedWeightKg = first.CalculatedWeightKg,
                    WeightDeviationKg = first.WeightDeviationKg,
                    CalculatedWeightFormula = BuildCalculatedWeightFormula(first.PassportWeightKg, first.MaterialCoefficient, first.CalculatedWeightKg),
                    WeightDeviationFormula = BuildWeightDeviationFormula(first.ActualWeightKg, first.PassportWeightKg, first.WeightDeviationKg),
                    SizeSummary = BuildLineSizeSummary(lineItems),
                    UsesAverageSize = lineItems.Any(x => x.IsSizeApproximate),
                };
            })
            .ToList();
    }

    private static string BuildMaterialsSummary(IEnumerable<ReceiptLineSummary> lines)
        => BuildMaterialsSummary(lines.Select(x => x.MaterialName));

    private static string BuildMaterialsSummary(IEnumerable<MetalReceiptDetailsLineViewModel> lines)
        => BuildMaterialsSummary(lines.Select(x => x.MaterialName));

    private static string BuildMaterialsSummary(IEnumerable<string> materialNames)
    {
        var names = materialNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (names.Count == 0)
        {
            return "-";
        }

        if (names.Count == 1)
        {
            return names[0];
        }

        return $"{names.Count} металла: {string.Join("; ", names.Take(3))}{(names.Count > 3 ? "; ..." : string.Empty)}";
    }

    private static string BuildReceiptSizeSummary(IEnumerable<ReceiptLineSummary> lines)
    {
        var totals = lines
            .Where(x => !string.IsNullOrWhiteSpace(x.UnitText))
            .GroupBy(x => x.UnitText)
            .Select(x => $"{FormatDecimal(x.Sum(line => line.TotalSize))} {x.Key}")
            .ToList();

        return totals.Count == 0 ? "-" : string.Join("; ", totals);
    }

    private static string BuildLineSizeSummary(IEnumerable<ReceiptItemSummaryProjection> lineItems)
    {
        var items = lineItems.ToList();
        if (items.Count == 0)
        {
            return "-";
        }

        var first = items[0];
        var unit = first.SizeUnitText;
        if (items.Any(x => x.IsSizeApproximate))
        {
            return $"примерно {FormatDecimal(first.SizeValue)} {unit}";
        }

        var min = items.Min(x => x.SizeValue);
        var max = items.Max(x => x.SizeValue);
        return min == max
            ? $"{FormatDecimal(min)} {unit}"
            : $"{FormatDecimal(min)}-{FormatDecimal(max)} {unit}";
    }

    private static string BuildLineSizeSummary(IEnumerable<ReceiptItemDetailsProjection> lineItems)
    {
        var items = lineItems.ToList();
        if (items.Count == 0)
        {
            return "-";
        }

        var first = items[0];
        var unit = first.SizeUnitText;
        if (items.Any(x => x.IsSizeApproximate))
        {
            return $"примерно {FormatDecimal(first.SizeValue)} {unit}";
        }

        var min = items.Min(x => x.SizeValue);
        var max = items.Max(x => x.SizeValue);
        return min == max
            ? $"{FormatDecimal(min)} {unit}"
            : $"{FormatDecimal(min)}-{FormatDecimal(max)} {unit}";
    }

    private static decimal CalculateWeightKg(MetalReceiptCreateViewModel model, MetalMaterial material)
    {
        return Math.Round((model.PassportWeightKg ?? 0m) * (material.Coefficient <= 0m ? 1m : material.Coefficient), 3);
    }

    private static decimal CalculateWeightKg(MetalReceiptLineInputViewModel line, MetalMaterial material)
    {
        return Math.Round((line.PassportWeightKg ?? 0m) * (material.Coefficient <= 0m ? 1m : material.Coefficient), 3);
    }

    private static (decimal SizeValue, string UnitText) ResolveSizeFromInputOrMass(decimal? sizeValue, MetalMaterial material, int quantity, decimal actualWeight)
    {
        var unitText = material.UnitKind == "SquareMeter" ? "м2" : "м";
        if (sizeValue.HasValue && sizeValue.Value > 0m)
        {
            return (sizeValue.Value, unitText);
        }

        var perItemWeight = quantity <= 0 ? 0m : actualWeight / quantity;
        if (unitText == "м2")
        {
            if (material.MassPerSquareMeterKg > 0m)
            {
                return (Math.Round(perItemWeight / material.MassPerSquareMeterKg, 3), "м2");
            }
        }

        if (material.MassPerMeterKg > 0m)
        {
            return (Math.Round(perItemWeight / material.MassPerMeterKg, 3), "м");
        }

        return (0m, unitText);
    }

    private static string ResolveStockCategory(string sizeUnitText, decimal sizeValue)
    {
        if (sizeValue <= 0m)
        {
            return "scrap";
        }

        return sizeUnitText switch
        {
            "м2" when sizeValue >= 1m => "whole",
            "м" when sizeValue >= 3m => "whole",
            _ when sizeValue >= 0.25m => "business",
            _ => "scrap",
        };
    }

    private static string BuildActualBlankSizeText(decimal sizeValue, string unitText, bool isApproximate = false)
    {
        var valueText = $"{Math.Round(sizeValue, 3).ToString("0.###", CultureInfo.InvariantCulture)} {unitText}";
        return isApproximate ? $"примерно {valueText}" : valueText;
    }

    private static string BuildCalculatedWeightFormula(decimal passportWeightKg, decimal coefficient, decimal calculatedWeightKg)
    {
        var safeCoefficient = coefficient <= 0m ? 1m : coefficient;
        return $"Расчётная масса = Паспортная масса × Коэффициент материала = {FormatDecimal(passportWeightKg)} × {FormatDecimal(safeCoefficient)} = {FormatDecimal(calculatedWeightKg)} кг";
    }

    private static string BuildWeightDeviationFormula(decimal actualWeightKg, decimal passportWeightKg, decimal deviationKg)
        => $"Отклонение = Фактическая масса - Паспортная масса = {FormatDecimal(actualWeightKg)} - {FormatDecimal(passportWeightKg)} = {FormatDecimal(deviationKg)} кг";

    private static string FormatDecimal(decimal value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string ToStockCategoryCaption(string stockCategory) =>
        stockCategory switch
        {
            "whole" => "Целая заготовка",
            "business" => "Деловой остаток",
            "scrap" => "Лом",
            _ => "В наличии",
        };

    private static string ResolveStockStatus(bool isConsumed, bool hasUsage, decimal currentSize)
    {
        if (isConsumed || currentSize <= 0m)
        {
            return "Израсходовано";
        }

        if (hasUsage)
        {
            return "Частично использовано";
        }

        return "В наличии";
    }

    private (Guid? UserId, string UserName) GetCurrentUserContext()
    {
        var userName = User?.Identity?.Name ?? "system";
        var userIdRaw = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out var userId)
            ? (userId, userName)
            : (null, userName);
    }

    private void AddAuditLog(string eventType, string entityType, Guid entityId, string? documentNumber, string message, object? payload = null)
    {
        var user = GetCurrentUserContext();
        _dbContext.MetalAuditLogs.Add(new MetalAuditLog
        {
            Id = Guid.NewGuid(),
            EventDate = DateTime.UtcNow,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            DocumentNumber = string.IsNullOrWhiteSpace(documentNumber) ? null : documentNumber,
            Message = message,
            UserId = user.UserId,
            UserName = user.UserName,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow,
        });
    }


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

    private static string NormalizeMaterialLookupText(string? value)
    {
        return string.Join(" ", (value ?? string.Empty).Trim().ToLowerInvariant().Split(new[] { " ", "\t", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeSupplierLookupText(string? value)
    {
        return NormalizeMaterialLookupText(value);
    }

    private static string BuildSupplierDisplay(string? identifier, string? name, string? inn, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(identifier) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(inn))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "—" : fallback;
        }

        var main = string.IsNullOrWhiteSpace(identifier)
            ? (name ?? string.Empty).Trim()
            : string.IsNullOrWhiteSpace(name)
                ? identifier.Trim()
                : $"{identifier.Trim()} · {name.Trim()}";

        return string.IsNullOrWhiteSpace(inn) ? main : $"{main} · ИНН {inn.Trim()}";
    }

    private static void NormalizeReceiptItems(MetalReceiptCreateViewModel model)
    {
        if (model.Items.Count == 0 && (model.MetalMaterialId.HasValue || model.Quantity.HasValue || model.PassportWeightKg.HasValue || model.Units.Count > 0))
        {
            model.Items.Add(new MetalReceiptLineInputViewModel
            {
                MetalMaterialId = model.MetalMaterialId,
                PassportWeightKg = model.PassportWeightKg,
                Quantity = model.Quantity,
                Units = model.Units,
            });
        }

        if (model.Items.Count == 0)
        {
            model.Items.Add(new MetalReceiptLineInputViewModel
            {
                Quantity = 1,
                Units = new List<MetalReceiptUnitInputViewModel>
                {
                    new() { ItemIndex = 1 },
                },
            });
        }

        foreach (var line in model.Items)
        {
            if (line.UseAverageSize)
            {
                line.Units.Clear();
                continue;
            }

            if (!line.Quantity.HasValue || line.Quantity.Value <= 0)
            {
                continue;
            }

            var quantity = line.Quantity.Value;
            if (line.Units.Count != quantity)
            {
                line.Units = Enumerable.Range(1, quantity)
                    .Select(i => new MetalReceiptUnitInputViewModel
                    {
                        ItemIndex = i,
                        SizeValue = line.Units.FirstOrDefault(x => x.ItemIndex == i)?.SizeValue,
                    })
                    .ToList();
                continue;
            }

            for (var i = 0; i < line.Units.Count; i++)
            {
                if (line.Units[i].ItemIndex <= 0)
                {
                    line.Units[i].ItemIndex = i + 1;
                }
            }
        }
    }

    private sealed record OriginalReceiptDocumentReadResult(
        bool IsValid,
        string? FileName,
        string? ContentType,
        byte[]? Content,
        long? SizeBytes,
        string? ErrorMessage);

    private static async Task<OriginalReceiptDocumentReadResult> ReadOriginalReceiptDocumentAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return new OriginalReceiptDocumentReadResult(true, null, null, null, null, null);
        }

        if (file.Length > MetalReceiptCreateViewModel.MaxOriginalDocumentSizeBytes)
        {
            return new OriginalReceiptDocumentReadResult(false, null, null, null, null, "Файл слишком большой. Максимум 25 МБ.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return new OriginalReceiptDocumentReadResult(false, null, null, null, null, "Можно прикрепить только DOCX-файл.");
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var content = memory.ToArray();

        if (content.Length < 4 || content[0] != 0x50 || content[1] != 0x4B)
        {
            return new OriginalReceiptDocumentReadResult(false, null, null, null, null, "Файл не похож на DOCX. Проверьте, что выбран документ Word.");
        }

        var safeFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "original.docx";
        }

        return new OriginalReceiptDocumentReadResult(
            true,
            safeFileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : file.ContentType,
            content,
            file.Length,
            null);
    }

    private async Task PopulateReceiptLookupsAsync(MetalReceiptCreateViewModel model, CancellationToken cancellationToken)
    {
        await PopulateMaterialsAsync(model, cancellationToken);
        await PopulateSuppliersAsync(model, cancellationToken);
        model.VatRatePercent = await GetVatRatePercentAsync(cancellationToken);
        RecalculateReceiptFinancials(model, model.VatRatePercent);
    }

    private async Task PopulateMaterialsAsync(MetalReceiptCreateViewModel model, CancellationToken cancellationToken)
    {
        var selectedMaterialIds = model.Items
            .Where(x => x.MetalMaterialId.HasValue)
            .Select(x => x.MetalMaterialId!.Value)
            .ToHashSet();

        model.Materials = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = x.Id.ToString(),
                Text = string.IsNullOrWhiteSpace(x.Code) ? x.Name : $"{x.Name} ({x.Code})",
                Selected = selectedMaterialIds.Contains(x.Id),
            })
            .ToListAsync(cancellationToken);

        model.MaterialUnitKinds = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.UnitKind })
            .ToDictionaryAsync(x => x.Id, x => x.UnitKind, cancellationToken);

        model.MaterialCoefficients = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Coefficient })
            .ToDictionaryAsync(x => x.Id, x => x.Coefficient > 0m ? x.Coefficient : 1m, cancellationToken);

        model.MaterialWeightPerUnitKg = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.WeightPerUnitKg, x.MassPerMeterKg, x.MassPerSquareMeterKg })
            .ToDictionaryAsync(
                x => x.Id,
                x => (x.WeightPerUnitKg ?? 0m) > 0m
                    ? x.WeightPerUnitKg!.Value
                    : (x.MassPerMeterKg > 0m ? x.MassPerMeterKg : (x.MassPerSquareMeterKg > 0m ? x.MassPerSquareMeterKg : 0m)),
                cancellationToken);

        if (model.Materials.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Нет доступных материалов для прихода. Добавьте материалы в справочник.");
        }
    }

    private async Task PopulateSuppliersAsync(MetalReceiptCreateViewModel model, CancellationToken cancellationToken)
    {
        var suppliers = await _dbContext.MetalSuppliers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        model.Suppliers = suppliers
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = BuildSupplierDisplay(x.Identifier, x.Name, x.Inn),
                Selected = model.SupplierId.HasValue && x.Id == model.SupplierId.Value,
            })
            .ToList();

        if (model.Suppliers.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Нет доступных поставщиков для прихода. Добавьте поставщиков в справочник.");
        }
    }

    private async Task<IReadOnlyCollection<MetalSupplierListItemViewModel>> GetSuppliersDirectoryItemsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.MetalSuppliers
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x => new MetalSupplierListItemViewModel
            {
                Id = x.Id,
                Identifier = x.Identifier,
                Name = x.Name,
                Inn = x.Inn,
                IsActive = x.IsActive,
            })
            .ToListAsync(cancellationToken);
    }

    private static void RecalculateReceiptFinancials(MetalReceiptCreateViewModel model, decimal vatRatePercent)
    {
        model.VatRatePercent = vatRatePercent;

        var passportWeightKg = model.Items.Count > 0
            ? model.Items.Where(x => x.PassportWeightKg.HasValue).Sum(x => x.PassportWeightKg!.Value)
            : model.PassportWeightKg ?? 0m;

        var amountWithoutVat = model.Items.Count > 0
            ? model.Items.Sum(x => (x.PassportWeightKg ?? 0m) * (x.PricePerKg ?? 0m))
            : passportWeightKg * (model.PricePerKg ?? 0m);
        model.AmountWithoutVat = Math.Round(amountWithoutVat, 2, MidpointRounding.AwayFromZero);
        model.VatAmount = Math.Round(model.AmountWithoutVat * vatRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        model.TotalAmountWithVat = model.AmountWithoutVat + model.VatAmount;
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

    private async Task EnsureMetalSuppliersSeededAsync(CancellationToken cancellationToken)
    {
        var hasActiveSuppliers = await _dbContext.MetalSuppliers.AnyAsync(x => x.IsActive, cancellationToken);
        if (hasActiveSuppliers)
        {
            return;
        }

        _dbContext.MetalSuppliers.Add(new MetalSupplier
        {
            Id = Guid.NewGuid(),
            Identifier = "00-001828",
            Name = "АО \"Металлоторг\"",
            Inn = "1234567890",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMetalReceiptParametersSeededAsync(CancellationToken cancellationToken)
    {
        var hasVatRate = await _dbContext.SystemParameters
            .AnyAsync(x => x.Key == VatRatePercentParameterKey, cancellationToken);

        if (hasVatRate)
        {
            return;
        }

        _dbContext.SystemParameters.Add(new SystemParameter
        {
            Key = VatRatePercentParameterKey,
            DecimalValue = DefaultVatRatePercent,
            Description = "Ставка НДС для прихода металла, %",
            UpdatedAt = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<decimal> GetVatRatePercentAsync(CancellationToken cancellationToken)
    {
        var value = await _dbContext.SystemParameters
            .AsNoTracking()
            .Where(x => x.Key == VatRatePercentParameterKey)
            .Select(x => x.DecimalValue)
            .FirstOrDefaultAsync(cancellationToken);

        return value is > 0m ? value.Value : DefaultVatRatePercent;
    }

    private async Task<string> GetNextReceiptNumberAsync(DateTime? receiptDate, CancellationToken cancellationToken)
    {
        const string prefix = "ПРИХОД-МЕТАЛЛА";
        var datePart = receiptDate?.ToString("yyyyMMdd") ?? DateTime.UtcNow.ToString("yyyyMMdd");

        var lastNumber = await _dbContext.MetalReceipts
            .AsNoTracking()
            .Where(x => x.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(x => x.ReceiptNumber)
            .Select(x => x.ReceiptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var numericPart = 0;
        if (!string.IsNullOrWhiteSpace(lastNumber))
        {
            var match = Regex.Match(lastNumber, @"-№(?<sequence>\d+)$");
            if (match.Success)
            {
                _ = int.TryParse(match.Groups["sequence"].Value, out numericPart);
            }
        }

        return $"{prefix}-{datePart}-№{(numericPart + 1):D4}";
    }

    private async Task<string> GetNextReceiptNumberAsync(
        MetalReceiptCreateViewModel model,
        MetalMaterial material,
        CancellationToken cancellationToken)
    {
        const string prefix = "ПРИХОД-МЕТАЛЛА";
        var datePart = model.ReceiptDate?.ToString("yyyyMMdd") ?? DateTime.UtcNow.ToString("yyyyMMdd");
        var materialPart = BuildMaterialCode(material);
        var quantity = model.Quantity ?? 0;
        var weight = model.PassportWeightKg ?? 0m;
        var unitKind = material.UnitKind == "SquareMeter" ? "м2" : "м";
        var sizeSummary = BuildSizeSummary(model);

        var lastNumber = await _dbContext.MetalReceipts
            .AsNoTracking()
            .Where(x => x.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(x => x.ReceiptNumber)
            .Select(x => x.ReceiptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var numericPart = 0;
        if (!string.IsNullOrWhiteSpace(lastNumber))
        {
            var match = Regex.Match(lastNumber, @"-№(?<sequence>\d+)$");
            if (match.Success)
            {
                _ = int.TryParse(match.Groups["sequence"].Value, out numericPart);
            }
        }

        return $"{prefix}-{datePart}-МАТЕРИАЛ_{materialPart}-КОЛ_{quantity}-ВЕС_{weight:0.###}кг-РАЗМЕР_{sizeSummary}{unitKind}-№{(numericPart + 1):D4}";
    }

    private static string BuildSizeSummary(MetalReceiptCreateViewModel model)
    {
        var sizes = model.Units
            .Where(x => x.SizeValue.HasValue && x.SizeValue.Value > 0m)
            .Select(x => x.SizeValue!.Value)
            .ToList();

        if (sizes.Count == 0)
        {
            return "НЕ_УКАЗАН";
        }

        var min = sizes.Min();
        var max = sizes.Max();
        return min == max
            ? FormatDecimal(min)
            : $"{FormatDecimal(min)}-{FormatDecimal(max)}";
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
    public Guid? MetalMaterialId { get; init; }

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

internal sealed class ReceiptItemSummaryProjection
{
    public int ReceiptLineIndex { get; init; }

    public int ItemIndex { get; init; }

    public Guid MetalMaterialId { get; init; }

    public string MaterialName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal PassportWeightKg { get; init; }

    public decimal SizeValue { get; init; }

    public string SizeUnitText { get; init; } = string.Empty;

    public bool IsSizeApproximate { get; init; }
}

internal sealed class ReceiptItemDetailsProjection
{
    public Guid Id { get; init; }

    public int ReceiptLineIndex { get; init; }

    public int ItemIndex { get; init; }

    public Guid MetalMaterialId { get; init; }

    public string MaterialName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal PassportWeightKg { get; init; }

    public decimal ActualWeightKg { get; init; }

    public decimal CalculatedWeightKg { get; init; }

    public decimal WeightDeviationKg { get; init; }

    public decimal MaterialCoefficient { get; init; }

    public decimal SizeValue { get; init; }

    public string SizeUnitText { get; init; } = string.Empty;

    public string ActualBlankSizeText { get; init; } = string.Empty;

    public bool IsSizeApproximate { get; init; }

    public string GeneratedCode { get; init; } = string.Empty;
}

internal sealed record ReceiptLineSummary(
    int LineIndex,
    string MaterialName,
    int Quantity,
    decimal PassportWeightKg,
    decimal TotalSize,
    string UnitText,
    string SizeSummary,
    bool UsesAverageSize);
