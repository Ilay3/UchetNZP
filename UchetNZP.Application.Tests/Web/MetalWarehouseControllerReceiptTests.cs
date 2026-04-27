using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Controllers;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class MetalWarehouseControllerReceiptTests
{
    [Fact]
    public async Task CreateReceipt_SavesPieceSizesAndActualBlankDimensions()
    {
        await using var dbContext = CreateContext();
        var material = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Лист 09Г2С t=4",
            Code = "LIST09G2S4",
            UnitKind = "SquareMeter",
            MassPerSquareMeterKg = 1.2m,
            StockUnit = "m2",
            IsActive = true,
        };
        dbContext.MetalMaterials.Add(material);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var model = new MetalReceiptCreateViewModel
        {
            ReceiptDate = new DateTime(2026, 4, 27),
            MetalMaterialId = material.Id,
            Quantity = 2,
            PassportWeightKg = 100m,
            Units = new List<MetalReceiptUnitInputViewModel>
            {
                new() { ItemIndex = 1, SizeValue = 2.500m },
                new() { ItemIndex = 2, SizeValue = 2.650m },
            },
        };

        var result = await controller.CreateReceipt(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MetalWarehouseController.ReceiptDetails), redirect.ActionName);

        var items = await dbContext.MetalReceiptItems
            .OrderBy(x => x.ItemIndex)
            .ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.Equal(2.500m, items[0].SizeValue);
        Assert.Equal(2.650m, items[1].SizeValue);
        Assert.All(items, x => Assert.Equal("м2", x.SizeUnitText));
        Assert.Equal("2.5 м2", items[0].ActualBlankSizeText);
        Assert.Equal("2.65 м2", items[1].ActualBlankSizeText);
    }

    [Fact]
    public async Task CreateReceipt_WhenSizeNotProvided_ComputesSizeFromMassAndStoresFallbackActualSizeText()
    {
        await using var dbContext = CreateContext();
        var material = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Пруток 20Г",
            Code = "PRUT20G",
            UnitKind = "Meter",
            MassPerMeterKg = 2m,
            StockUnit = "m",
            IsActive = true,
        };
        dbContext.MetalMaterials.Add(material);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var model = new MetalReceiptCreateViewModel
        {
            ReceiptDate = new DateTime(2026, 4, 27),
            MetalMaterialId = material.Id,
            Quantity = 2,
            PassportWeightKg = 8m,
            Units = new List<MetalReceiptUnitInputViewModel>
            {
                new() { ItemIndex = 1, SizeValue = null },
                new() { ItemIndex = 2, SizeValue = null },
            },
        };

        var result = await controller.CreateReceipt(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var items = await dbContext.MetalReceiptItems
            .OrderBy(x => x.ItemIndex)
            .ToListAsync();

        Assert.Equal(2, items.Count);
        Assert.All(items, x => Assert.Equal(2m, x.SizeValue));
        Assert.All(items, x => Assert.Equal("м", x.SizeUnitText));
        Assert.All(items, x => Assert.Equal("2 м", x.ActualBlankSizeText));
    }

    [Fact]
    public async Task ReceiptDetails_ReturnsFormulaTooltipsForCalculatedWeightAndDeviation()
    {
        await using var dbContext = CreateContext();
        var material = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Круг 45",
            Code = "KRUG45",
            UnitKind = "Meter",
            MassPerMeterKg = 2m,
            Coefficient = 1.05m,
            StockUnit = "m",
            IsActive = true,
        };

        var receipt = new MetalReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = "MET-000123",
            ReceiptDate = new DateTime(2026, 4, 27),
            Comment = "Тест",
            BatchNumber = string.Empty,
        };

        var item = new MetalReceiptItem
        {
            Id = Guid.NewGuid(),
            MetalReceiptId = receipt.Id,
            MetalReceipt = receipt,
            MetalMaterialId = material.Id,
            MetalMaterial = material,
            ItemIndex = 1,
            Quantity = 1,
            SizeValue = 3m,
            SizeUnitText = "м",
            ActualBlankSizeText = "3 м",
            PassportWeightKg = 10m,
            ActualWeightKg = 10m,
            CalculatedWeightKg = 10.5m,
            WeightDeviationKg = 0m,
            TotalWeightKg = 10m,
            StockCategory = "whole",
            GeneratedCode = "KRUG45-3-M-001",
            CreatedAt = new DateTime(2026, 4, 27),
        };

        dbContext.MetalMaterials.Add(material);
        dbContext.MetalReceipts.Add(receipt);
        dbContext.MetalReceiptItems.Add(item);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.ReceiptDetails(receipt.Id, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MetalReceiptDetailsViewModel>(view.Model);
        Assert.Equal(
            "Расчётная масса = Паспортная масса × Коэффициент материала = 10 × 1.05 = 10.5 кг",
            model.CalculatedWeightFormula);
        Assert.Equal(
            "Отклонение = Фактическая масса - Паспортная масса = 10 - 10 = 0 кг",
            model.WeightDeviationFormula);
    }

    private static MetalWarehouseController CreateController(AppDbContext dbContext)
    {
        var controller = new MetalWarehouseController(
            dbContext,
            new NoOpCuttingMapExcelExporter(),
            new NoOpCuttingMapPdfExporter(),
            new NoOpWarehousePrintService(),
            new NoOpMetalReceiptItemLabelDocumentService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };

        controller.TempData = new TempDataDictionary(controller.HttpContext, new TestTempDataProvider());
        return controller;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class NoOpCuttingMapExcelExporter : ICuttingMapExcelExporter
    {
        public byte[] Export(CuttingMapCardViewModel map) => Array.Empty<byte>();
    }

    private sealed class NoOpCuttingMapPdfExporter : ICuttingMapPdfExporter
    {
        public byte[] Export(CuttingMapCardViewModel map) => Array.Empty<byte>();
    }

    private sealed class NoOpWarehousePrintService : IMetalRequirementWarehousePrintDocumentService
    {
        public Task<MetalRequirementWarehousePrintDocumentResult> BuildAsync(Guid requirementId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalRequirementWarehousePrintDocumentResult("dummy.docx", Array.Empty<byte>()));
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    private sealed class NoOpMetalReceiptItemLabelDocumentService : IMetalReceiptItemLabelDocumentService
    {
        public Task<MetalReceiptItemLabelDocumentResult> BuildAsync(Guid receiptItemId, string qrTarget, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalReceiptItemLabelDocumentResult("label.pdf", "application/pdf", Array.Empty<byte>(), qrTarget));
    }
}
