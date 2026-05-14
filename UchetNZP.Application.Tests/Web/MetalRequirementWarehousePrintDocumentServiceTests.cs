using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Services;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class MetalRequirementWarehousePrintDocumentServiceTests
{
    [Fact]
    public async Task BuildAsync_FillsRequirementInvoiceTemplate()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var launchId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        var now = new DateTime(2026, 5, 14, 8, 0, 0, DateTimeKind.Utc);

        var material = new MetalMaterial
        {
            Id = materialId,
            Name = "Steel sheet",
            Code = "MAT-001",
            UnitKind = "SquareMeter",
            StockUnit = "m2",
            WeightPerUnitKg = 1m,
            IsActive = true,
        };

        dbContext.Parts.Add(new Part { Id = partId, Name = "Part 001", Code = "P-001" });
        dbContext.Sections.Add(new Section { Id = sectionId, Name = "Assembly", Code = "ASM" });
        dbContext.MetalMaterials.Add(material);
        dbContext.WipLaunches.Add(new WipLaunch
        {
            Id = launchId,
            UserId = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            FromOpNumber = 10,
            LaunchDate = now,
            CreatedAt = now,
            Quantity = 5m,
            SumHoursToFinish = 1m,
        });

        dbContext.MetalRequirements.Add(new MetalRequirement
        {
            Id = requirementId,
            RequirementNumber = "MR-TEST-001",
            RequirementDate = now,
            Status = "Created",
            WipLaunchId = launchId,
            PartId = partId,
            PartCode = "P-001",
            PartName = "Part 001",
            Quantity = 5m,
            MetalMaterialId = materialId,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "master",
            UpdatedBy = "storekeeper",
            Items =
            {
                new MetalRequirementItem
                {
                    Id = Guid.NewGuid(),
                    MetalMaterialId = materialId,
                    MetalMaterial = material,
                    ConsumptionPerUnit = 2.5m,
                    ConsumptionUnit = "m2",
                    RequiredQty = 12.5m,
                    RequiredWeightKg = 12.5m,
                    NormPerUnit = 2.5m,
                    TotalRequiredQty = 12.5m,
                    TotalRequiredWeightKg = 12.5m,
                    Unit = "m2",
                    SizeRaw = "1250x2500",
                },
            },
        });
        await dbContext.SaveChangesAsync();

        var environment = new TestWebHostEnvironment(ResolveWebContentRoot());
        var service = new MetalRequirementWarehousePrintDocumentService(dbContext, environment);

        var result = await service.BuildAsync(requirementId);

        Assert.EndsWith(".docx", result.FileName, StringComparison.OrdinalIgnoreCase);
        using var stream = new MemoryStream(result.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var mainDocument = document.MainDocumentPart?.Document ?? throw new InvalidOperationException("Generated document has no main document.");
        var body = mainDocument.Body ?? throw new InvalidOperationException("Generated document has no body.");
        var text = string.Join("\n", body.Descendants<Text>().Select(x => x.Text));

        Assert.Contains("MR-TEST-001", text);
        Assert.Contains("MAT-001", text);
        Assert.Contains("Steel sheet", text);
        Assert.Contains("12.5", text);
        Assert.Contains("10.01", text);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static string ResolveWebContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "UchetNZP.Web");
            if (Directory.Exists(Path.Combine(candidate, "Templates", "Documents")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("UchetNZP.Web content root was not found.");
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "UchetNZP.Web";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; } = Path.Combine(contentRootPath, "wwwroot");

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
