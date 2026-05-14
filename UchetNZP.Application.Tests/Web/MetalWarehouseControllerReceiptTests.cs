using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
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
        var supplier = CreateSupplier();
        dbContext.MetalMaterials.Add(material);
        dbContext.MetalSuppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var model = new MetalReceiptCreateViewModel
        {
            ReceiptDate = new DateTime(2026, 4, 27),
            SupplierId = supplier.Id,
            SupplierDocumentNumber = "DOC-001",
            PricePerKg = 114.04m,
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
        var supplier = CreateSupplier();
        dbContext.MetalSuppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var model = new MetalReceiptCreateViewModel
        {
            ReceiptDate = new DateTime(2026, 4, 27),
            SupplierId = supplier.Id,
            SupplierDocumentNumber = "DOC-002",
            PricePerKg = 114.04m,
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
        var supplier = CreateSupplier();
        dbContext.MetalMaterials.AddRange(sheet, rod);
        dbContext.MetalSuppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        var docxBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4 };
        await using var docxStream = new MemoryStream(docxBytes);
        var formFile = new FormFile(docxStream, 0, docxBytes.Length, nameof(MetalReceiptCreateViewModel.OriginalDocumentPdf), "scan.docx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        };

        var controller = CreateController(dbContext);
        var model = new MetalReceiptCreateViewModel
        {
            ReceiptDate = new DateTime(2026, 4, 27),
            SupplierId = supplier.Id,
            SupplierDocumentNumber = "DOC-003",
            PricePerKg = 114.04m,
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
        Assert.Equal("scan.docx", receipt.OriginalDocumentFileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", receipt.OriginalDocumentContentType);
        Assert.Equal(docxBytes, receipt.OriginalDocumentContent);
        Assert.Equal(supplier.Id, receipt.MetalSupplierId);
        Assert.Equal("DOC-003", receipt.SupplierDocumentNumber);
        Assert.Equal(114.04m, receipt.PricePerKg);
        Assert.Equal(15509.44m, receipt.AmountWithoutVat);
        Assert.Equal(3412.08m, receipt.VatAmount);
        Assert.Equal(18921.52m, receipt.TotalAmountWithVat);

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
        Assert.Equal("scan.docx", file.FileDownloadName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.ContentType);
        Assert.Equal(docxBytes, file.FileContents);
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
            new NoOpMetalReceiptDocumentService(),
            NullLogger<MetalWarehouseController>.Instance)
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

    private static MetalSupplier CreateSupplier()
    {
        return new MetalSupplier
        {
            Id = Guid.NewGuid(),
            Identifier = "00-001828",
            Name = "АО \"Металлоторг\"",
            Inn = "1234567890",
            IsActive = true,
            CreatedAt = new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc),
        };
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

        public Task<MetalRequirementWarehousePrintDocumentResult> BuildPdfAsync(Guid requirementId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalRequirementWarehousePrintDocumentResult("dummy.pdf", Array.Empty<byte>(), "application/pdf"));
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
            => Task.FromResult(new MetalReceiptItemLabelDocumentResult("label.pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Array.Empty<byte>(), receiptItemId.ToString()));
    }

    private sealed class NoOpMetalReceiptDocumentService : IMetalReceiptDocumentService
    {
        public Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalReceiptDocumentResult("receipt.pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Array.Empty<byte>()));

        public Task<MetalReceiptDocumentResult> BuildPdfAsync(Guid receiptId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalReceiptDocumentResult("receipt.pdf", "application/pdf", Array.Empty<byte>()));
    }
}
