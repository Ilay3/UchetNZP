using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("MetalWarehouse")]
public class MetalWarehouseController : Controller
{
    private readonly AppDbContext _dbContext;

    public MetalWarehouseController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new MetalWarehouseDashboardViewModel
        {
            MaterialsInCatalog = await _dbContext.Parts.AsNoTracking().CountAsync(cancellationToken),
            MetalUnitsInStock = await _dbContext.WarehouseItems.AsNoTracking().SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m,
            OpenRequirements = 0,
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

        if (material is null)
        {
            return BadRequest("Материал не найден.");
        }

        var now = DateTime.UtcNow;
        var nextNumber = await GetNextReceiptNumberAsync(cancellationToken);
        var receipt = new MetalReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = nextNumber,
            ReceiptDate = model.ReceiptDate!.Value.Date,
            SupplierOrSource = model.SupplierOrSource?.Trim(),
            Comment = model.Comment?.Trim(),
            CreatedAt = now,
        };

        var materialCode = BuildMaterialCode(material);
        var quantity = model.Quantity!.Value;
        var totalWeight = model.TotalWeightKg!.Value;
        for (var i = 0; i < quantity; i++)
        {
            var unit = model.Units[i];
            var suffix = (i + 1).ToString("D3");
            var sizeUnitText = material.UnitKind == "SquareMeter" ? "м2" : "м";
            var sizePart = unit.SizeValue!.Value.ToString("0.###").Replace('.', '_');
            receipt.Items.Add(new MetalReceiptItem
            {
                Id = Guid.NewGuid(),
                MetalMaterialId = material.Id,
                Quantity = quantity,
                TotalWeightKg = totalWeight,
                ItemIndex = i + 1,
                SizeValue = unit.SizeValue!.Value,
                SizeUnitText = sizeUnitText,
                GeneratedCode = $"{materialCode}-{sizePart}-{(sizeUnitText == "м2" ? "M2" : "M")}-{suffix}",
                CreatedAt = now,
            });
        }

        _dbContext.MetalReceipts.Add(receipt);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["MetalReceiptSuccess"] = $"Приход {receipt.ReceiptNumber} успешно создан.";

        return RedirectToAction(nameof(Receipts));
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
                x.Comment,
                Item = x.Items
                    .OrderBy(i => i.ItemIndex)
                    .Select(i => new
                    {
                        i.ItemIndex,
                        i.SizeValue,
                        i.SizeUnitText,
                        i.GeneratedCode,
                        i.TotalWeightKg,
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
            TotalWeightKg = first.TotalWeightKg,
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
                WeightKg = x.Quantity > 0 ? x.TotalWeightKg / x.Quantity : x.TotalWeightKg,
                ReceiptNumber = x.MetalReceipt != null ? x.MetalReceipt.ReceiptNumber : string.Empty,
                ReceiptDate = x.MetalReceipt != null ? x.MetalReceipt.ReceiptDate : x.CreatedAt,
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
            .OrderByDescending(x => x.ReceiptDate)
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
    public IActionResult Requirements()
    {
        var model = new MetalWarehouseListPageViewModel
        {
            Title = "Требования производства",
            Description = "Запросы цеха на выдачу металла.",
            Headers = new[] { "№ требования", "Дата", "Цех", "Материал", "Требуемо", "Статус" },
            EmptyStateTitle = "Открытых требований нет",
            EmptyStateDescription = "Новые требования появятся здесь после подключения процесса выдачи.",
        };

        return View(model);
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
    }

    private async Task EnsureMetalMaterialsSeededAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.MetalMaterials.AnyAsync(cancellationToken))
        {
            return;
        }

        var seed = new[]
        {
            new MetalMaterial { Id = Guid.NewGuid(), Name = "Лист ст.35 t=6 ГОСТ19903-74/1577-93", Code = "LIST35T6", UnitKind = "SquareMeter", IsActive = true },
            new MetalMaterial { Id = Guid.NewGuid(), Name = "Пруток 20Г", Code = "PRUT20G", UnitKind = "Meter", IsActive = true },
            new MetalMaterial { Id = Guid.NewGuid(), Name = "Круг 45", Code = "KRUG45", UnitKind = "Meter", IsActive = true },
            new MetalMaterial { Id = Guid.NewGuid(), Name = "Лист 09Г2С t=4", Code = "LIST09G2S4", UnitKind = "SquareMeter", IsActive = true },
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
