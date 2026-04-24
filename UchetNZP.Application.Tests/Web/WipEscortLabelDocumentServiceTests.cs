using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Services;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class WipEscortLabelDocumentServiceTests
{
    [Fact]
    public async Task BuildAsync_WithFourOperations_CreatesExactlyFourOperationRows()
    {
        var (dbContext, receiptId, contentRootPath) = await CreateContextWithReceiptAsync(operationsCount: 4);
        CreateTemplate(contentRootPath);
        var service = new WipEscortLabelDocumentService(dbContext, new TestWebHostEnvironment(contentRootPath));

        var bytes = await service.BuildAsync(receiptId);

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var table = document.MainDocumentPart!.Document.Body!.Descendants<Table>().First();
        var operationRows = table.Descendants<TableRow>()
            .Skip(1)
            .Select(row => row.Elements<TableCell>().ToList())
            .Where(cells => cells.Count >= 3 && !string.IsNullOrWhiteSpace(GetCellText(cells[1])) && !string.IsNullOrWhiteSpace(GetCellText(cells[2])))
            .ToList();

        Assert.Equal(4, operationRows.Count);
        Assert.DoesNotContain(operationRows, row => GetCellText(row[0]).Length > 0);
    }


    [Fact]
    public async Task BuildAsync_WhenOperationPlaceholdersSplitAcrossRuns_FillsOperationNumberAndName()
    {
        var (dbContext, receiptId, contentRootPath) = await CreateContextWithReceiptAsync(operationsCount: 2);
        CreateTemplate(contentRootPath, splitOperationTokensAcrossRuns: true);
        var service = new WipEscortLabelDocumentService(dbContext, new TestWebHostEnvironment(contentRootPath));

        var bytes = await service.BuildAsync(receiptId);

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var table = document.MainDocumentPart!.Document.Body!.Descendants<Table>().First();
        var operationRows = table.Descendants<TableRow>()
            .Skip(1)
            .Select(row => row.Elements<TableCell>().ToList())
            .ToList();

        Assert.Equal(2, operationRows.Count);
        Assert.Equal("10", GetCellText(operationRows[0][1]));
        Assert.Equal("Операция 1", GetCellText(operationRows[0][2]));
        Assert.Equal("20", GetCellText(operationRows[1][1]));
        Assert.Equal("Операция 2", GetCellText(operationRows[1][2]));
    }

    [Fact]
    public async Task BuildAsync_UsesLabelNumberFromReceipt()
    {
        var (dbContext, receiptId, contentRootPath) = await CreateContextWithReceiptAsync(operationsCount: 1, labelNumber: "LBL-DETAIL-77");
        CreateTemplate(contentRootPath);
        var service = new WipEscortLabelDocumentService(dbContext, new TestWebHostEnvironment(contentRootPath));

        var bytes = await service.BuildAsync(receiptId);

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var documentText = string.Concat(document.MainDocumentPart!.Document.Body!.Descendants<Text>().Select(x => x.Text));

        Assert.Contains("LBL-DETAIL-77", documentText);
    }

    [Fact]
    public async Task BuildAsync_WhenMaterialSizeExists_FillsSizeInsteadOfMaterialCode()
    {
        var (dbContext, receiptId, contentRootPath) = await CreateContextWithReceiptAsync(operationsCount: 1, materialCode: "LIST35T6");
        await AddMetalReceiptItemAsync(dbContext, sizeValue: 74m, sizeUnit: "мм");
        CreateTemplate(contentRootPath, includeMaterialSizeToken: true);
        var service = new WipEscortLabelDocumentService(dbContext, new TestWebHostEnvironment(contentRootPath));

        var bytes = await service.BuildAsync(receiptId);

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var documentText = string.Concat(document.MainDocumentPart!.Document.Body!.Descendants<Text>().Select(x => x.Text));

        Assert.Contains("Размер: 74 мм", documentText);
        Assert.DoesNotContain("LIST35T6", documentText);
    }

    private static string GetCellText(TableCell cell)
    {
        return string.Concat(cell.Descendants<Text>().Select(x => x.Text)).Trim();
    }

    private static void CreateTemplate(string contentRootPath, bool splitOperationTokensAcrossRuns = false, bool includeMaterialSizeToken = false)
    {
        var documentDir = Path.Combine(contentRootPath, "Templates", "Documents");
        Directory.CreateDirectory(documentDir);
        var templatePath = Path.Combine(documentDir, "Сопроводительный ярлык.docx");

        using var document = WordprocessingDocument.Create(templatePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        var bodyChildren = new List<OpenXmlElement>
        {
            new Paragraph(new Run(new Text("Ярлык: {{LBL}}"))),
            new Table(
                new TableRow(
                    CreateCell("Подразделение"),
                    CreateCell("№"),
                    CreateCell("Операция"),
                    CreateCell("Колонка 4")),
                new TableRow(
                    CreateCell("ПУСТО"),
                    splitOperationTokensAcrossRuns ? CreateCellFromRuns("{{OP_", "NO}}") : CreateCell("{{OP_NO}}"),
                    splitOperationTokensAcrossRuns ? CreateCellFromRuns("{{OP_", "NAME}}") : CreateCell("{{OP_NAME}}"),
                    CreateCell("ДОЛЖНО_БЫТЬ_ПУСТО"),
                    CreateCell(string.Empty)))
        };

        if (includeMaterialSizeToken)
        {
            bodyChildren.Add(new Paragraph(new Run(new Text("Размер: {{MAT_SIZE}}"))));
        }

        var body = new Body(bodyChildren);
        main.Document = new Document(body);
        main.Document.Save();
    }

    private static TableCell CreateCell(string text)
    {
        return new TableCell(new Paragraph(new Run(new Text(text))));
    }

    private static TableCell CreateCellFromRuns(params string[] runs)
    {
        var paragraph = new Paragraph();
        foreach (var runText in runs)
        {
            paragraph.Append(new Run(new Text(runText)));
        }

        return new TableCell(paragraph);
    }

    private static async Task<(AppDbContext DbContext, Guid ReceiptId, string ContentRootPath)> CreateContextWithReceiptAsync(int operationsCount, string labelNumber = "LBL-001", string? materialCode = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var dbContext = new AppDbContext(options);
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        dbContext.Parts.Add(new Part { Id = partId, Name = "Деталь", Code = "КД-001" });
        dbContext.Sections.Add(new Section { Id = sectionId, Name = "Цех 1" });
        dbContext.WipLabels.Add(new WipLabel
        {
            Id = labelId,
            PartId = partId,
            LabelDate = new DateTime(2026, 1, 1),
            LabelYear = 2026,
            Quantity = 10,
            RemainingQuantity = 10,
            Number = labelNumber,
            RootLabelId = labelId,
            RootNumber = labelNumber,
            Suffix = 0,
        });
        dbContext.MetalMaterials.Add(new MetalMaterial { Id = materialId, Name = "Сталь", Code = materialCode, UnitKind = "Meter", IsActive = true });
        dbContext.WipReceipts.Add(new WipReceipt
        {
            Id = receiptId,
            UserId = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 10,
            ReceiptDate = new DateTime(2026, 1, 1),
            CreatedAt = new DateTime(2026, 1, 1),
            Quantity = 3,
            WipLabelId = labelId,
            MetalMaterialId = materialId,
        });

        for (var i = 1; i <= operationsCount; i++)
        {
            var operationId = Guid.NewGuid();
            dbContext.Operations.Add(new Operation { Id = operationId, Name = $"Операция {i}" });
            dbContext.PartRoutes.Add(new PartRoute
            {
                Id = Guid.NewGuid(),
                PartId = partId,
                SectionId = sectionId,
                OperationId = operationId,
                OpNumber = i * 10,
                NormHours = 1,
            });
        }

        await dbContext.SaveChangesAsync();

        var contentRootPath = Path.Combine(Path.GetTempPath(), "escort-label-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRootPath);
        return (dbContext, receiptId, contentRootPath);
    }

    private static async Task AddMetalReceiptItemAsync(AppDbContext dbContext, decimal sizeValue, string sizeUnit)
    {
        var materialId = await dbContext.WipReceipts
            .Select(x => x.MetalMaterialId)
            .FirstAsync();
        if (!materialId.HasValue)
        {
            return;
        }

        var metalReceiptId = Guid.NewGuid();
        dbContext.MetalReceipts.Add(new MetalReceipt
        {
            Id = metalReceiptId,
            ReceiptNumber = "MR-001",
            ReceiptDate = new DateTime(2026, 1, 2),
            CreatedAt = new DateTime(2026, 1, 2),
        });
        dbContext.MetalReceiptItems.Add(new MetalReceiptItem
        {
            Id = Guid.NewGuid(),
            MetalReceiptId = metalReceiptId,
            MetalMaterialId = materialId.Value,
            Quantity = 1,
            TotalWeightKg = 1,
            ItemIndex = 1,
            SizeValue = sizeValue,
            SizeUnitText = sizeUnit,
            GeneratedCode = "GEN",
            CreatedAt = new DateTime(2026, 1, 2),
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "UchetNZP.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
