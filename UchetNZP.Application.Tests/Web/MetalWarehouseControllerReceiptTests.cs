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
            ProfileType = "sheet",
            WidthMm = 1500m,
            LengthMm = 3000m,
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
        Assert.All(items, x => Assert.Equal("1500 мм × 3000 мм", x.ActualBlankSizeText));
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
            ProfileType = "rod",
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

    private static MetalWarehouseController CreateController(AppDbContext dbContext)
    {
        var controller = new MetalWarehouseController(
            dbContext,
            new NoOpCuttingMapExcelExporter(),
            new NoOpCuttingMapPdfExporter(),
            new NoOpWarehousePrintService())
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
}
