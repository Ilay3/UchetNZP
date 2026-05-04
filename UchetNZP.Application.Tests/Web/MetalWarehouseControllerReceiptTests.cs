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

        var result = await controller.CreateReceipt(model, string.Empty, CancellationToken.None);

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

        var result = await controller.CreateReceipt(model, string.Empty, CancellationToken.None);

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
    public async Task CreateReceipt_SavesMultipleMaterialsAverageSizeAndOriginalPdf()
    {
        await using var dbContext = CreateContext();
        var sheet = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Лист 09Г2С t=4",
            Code = "LIST09G2S4",
            UnitKind = "SquareMeter",
            MassPerSquareMeterKg = 1.2m,
            StockUnit = "m2",
            IsActive = true,
        };
        var rod = new MetalMaterial
        {
            Id = Guid.NewGuid(),
            Name = "Пруток 20Г",
            Code = "PRUT20G",
            UnitKind = "Meter",
            MassPerMeterKg = 2m,
            StockUnit = "m",
            IsActive = true,
        };
        dbContext.MetalMaterials.AddRange(sheet, rod);
        await dbContext.SaveChangesAsync();

        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 1, 2, 3 };
        await using var pdfStream = new MemoryStream(pdfBytes);
        var formFile = new FormFile(pdfStream, 0, pdfBytes.Length, nameof(MetalReceiptCreateViewModel.OriginalDocumentPdf), "scan.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };

        var controller = CreateController(dbContext);
        var model = new MetalReceiptCreateViewModel
        {
            ReceiptDate = new DateTime(2026, 4, 27),
            OriginalDocumentPdf = formFile,
            Items = new List<MetalReceiptLineInputViewModel>
            {
                new()
                {
                    MetalMaterialId = sheet.Id,
                    Quantity = 2,
                    PassportWeightKg = 100m,
                    Units = new List<MetalReceiptUnitInputViewModel>
                    {
                        new() { ItemIndex = 1, SizeValue = 2.5m },
                        new() { ItemIndex = 2, SizeValue = 2.6m },
                    },
                },
                new()
                {
                    MetalMaterialId = rod.Id,
                    Quantity = 3,
                    PassportWeightKg = 36m,
                    UseAverageSize = true,
                    AverageSizeValue = 6m,
                },
            },
        };

        var result = await controller.CreateReceipt(model, string.Empty, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var receipt = await dbContext.MetalReceipts.SingleAsync();
        Assert.Equal("scan.pdf", receipt.OriginalDocumentFileName);
        Assert.Equal("application/pdf", receipt.OriginalDocumentContentType);
        Assert.Equal(pdfBytes, receipt.OriginalDocumentContent);

        var items = await dbContext.MetalReceiptItems
            .OrderBy(x => x.ItemIndex)
            .ToListAsync();
        Assert.Equal(5, items.Count);
        Assert.Equal(new[] { 1, 1, 2, 2, 2 }, items.Select(x => x.ReceiptLineIndex).ToArray());
        Assert.False(items[0].IsSizeApproximate);
        Assert.All(items.Skip(2), x =>
        {
            Assert.True(x.IsSizeApproximate);
            Assert.Equal(6m, x.SizeValue);
            Assert.Equal("примерно 6 м", x.ActualBlankSizeText);
        });

        var original = await controller.ReceiptOriginalDocument(receipt.Id, CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(original);
        Assert.Equal("scan.pdf", file.FileDownloadName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(pdfBytes, file.FileContents);
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
            new NoOpMetalReceiptItemLabelDocumentService(),
            new NoOpMetalReceiptDocumentService())
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
        public Task<MetalReceiptItemLabelDocumentResult> BuildAsync(Guid receiptItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalReceiptItemLabelDocumentResult("label.pdf", "application/pdf", Array.Empty<byte>(), receiptItemId.ToString()));
    }

    private sealed class NoOpMetalReceiptDocumentService : IMetalReceiptDocumentService
    {
        public Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalReceiptDocumentResult("receipt.pdf", "application/pdf", Array.Empty<byte>()));
    }
}
