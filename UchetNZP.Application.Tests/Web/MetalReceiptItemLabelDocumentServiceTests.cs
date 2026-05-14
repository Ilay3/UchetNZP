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

public class MetalReceiptItemLabelDocumentServiceTests
{
    [Fact]
    public async Task BuildAsync_ReturnsPdfLabelAndKeepsQrPayload()
    {
        await using var dbContext = CreateContext();
        var materialId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        dbContext.MetalMaterials.Add(new MetalMaterial
        {
            Id = materialId,
            Name = "Лист ст.35 t=6",
            Code = "LIST35T6",
            UnitKind = "SquareMeter",
            StockUnit = "m2",
            IsActive = true,
        });

        dbContext.MetalReceipts.Add(new MetalReceipt
        {
            Id = receiptId,
            ReceiptNumber = "MR-100",
            ReceiptDate = new DateTime(2026, 4, 27),
            CreatedAt = new DateTime(2026, 4, 27),
        });

        dbContext.MetalReceiptItems.Add(new MetalReceiptItem
        {
            Id = itemId,
            MetalReceiptId = receiptId,
            MetalMaterialId = materialId,
            Quantity = 1,
            TotalWeightKg = 10,
            ItemIndex = 1,
            SizeValue = 1.5m,
            SizeUnitText = "м2",
            ActualBlankSizeText = "1500 мм × 3000 мм",
            GeneratedCode = "LIST35T6-001",
            CreatedAt = new DateTime(2026, 4, 27),
        });

        await dbContext.SaveChangesAsync();

        var service = new MetalReceiptItemLabelDocumentService(dbContext);

        var result = await service.BuildAsync(itemId);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Contains("LIST35T6-001", result.QrPayload, StringComparison.Ordinal);
        Assert.Contains("LIST35T6-001", result.FileName);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task ReceiptItemLabel_ReturnsGeneratedItemLabelFile()
    {
        await using var dbContext = CreateContext();
        var fakeLabelService = new CapturingMetalReceiptItemLabelDocumentService();
        var controller = new MetalWarehouseController(
            dbContext,
            new NoOpCuttingMapExcelExporter(),
            new NoOpCuttingMapPdfExporter(),
            new NoOpWarehousePrintService(),
            fakeLabelService,
            new NoOpMetalReceiptDocumentService(),
            NullLogger<MetalWarehouseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("example.local");
        controller.TempData = new TempDataDictionary(controller.HttpContext, new TestTempDataProvider());

        var itemId = Guid.NewGuid();

        var result = await controller.ReceiptItemLabel(itemId, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal(itemId, fakeLabelService.LastReceiptItemId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class CapturingMetalReceiptItemLabelDocumentService : IMetalReceiptItemLabelDocumentService
    {
        public Guid? LastReceiptItemId { get; private set; }

        public Task<MetalReceiptItemLabelDocumentResult> BuildAsync(Guid receiptItemId, CancellationToken cancellationToken = default)
        {
            LastReceiptItemId = receiptItemId;
            return Task.FromResult(new MetalReceiptItemLabelDocumentResult("label.pdf", "application/pdf", new byte[] { 1, 2, 3 }, receiptItemId.ToString()));
        }
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

    private sealed class NoOpMetalReceiptDocumentService : IMetalReceiptDocumentService
    {
        public Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalReceiptDocumentResult("receipt.pdf", "application/pdf", Array.Empty<byte>()));

        public Task<MetalReceiptDocumentResult> BuildPdfAsync(Guid receiptId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetalReceiptDocumentResult("receipt.pdf", "application/pdf", Array.Empty<byte>()));
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
