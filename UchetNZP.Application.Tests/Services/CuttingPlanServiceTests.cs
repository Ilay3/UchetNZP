using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Contracts.Cutting;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class CuttingPlanServiceTests
{
    [Fact]
    public async Task BuildAndSaveAsync_ForLinear_CalculatesKpiAndVersioning()
    {
        await using var dbContext = CreateContext();
        var requirementId = await SeedRequirementAsync(dbContext);
        var service = new CuttingPlanService(dbContext);

        var request = new SaveCuttingPlanRequest(
            requirementId,
            new LinearCutRequest(6000m, 3m, 500m, [new LinearCutPartRequest(1200m, 5), new LinearCutPartRequest(900m, 2)]),
            null);

        var first = await service.BuildAndSaveAsync(request);
        var second = await service.BuildAndSaveAsync(request with { Linear = request.Linear! with { Kerf = 4m } });

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.True(first.UtilizationPercent > 0m);
        Assert.Equal(first.UtilizationPercent + first.WastePercent, 100m, 3);

        var currentPlans = await dbContext.CuttingPlans.CountAsync(x => x.MetalRequirementId == requirementId && x.IsCurrent);
        Assert.Equal(1, currentPlans);
    }

    [Fact]
    public async Task BuildAndSaveAsync_ForSheet_BuildsPlanWithRotation()
    {
        await using var dbContext = CreateContext();
        var requirementId = await SeedRequirementAsync(dbContext);
        var service = new CuttingPlanService(dbContext);

        var result = await service.BuildAndSaveAsync(new SaveCuttingPlanRequest(
            requirementId,
            null,
            new SheetCutRequest(3000m, 1500m, 10m, 5m, [new SheetCutPartRequest(1000m, 700m, 4), new SheetCutPartRequest(800m, 600m, 2)], true)));

        Assert.Equal("TwoDimensional", result.Kind);
        Assert.NotEmpty(result.Stocks);
        Assert.True(result.CutCount > 0);
        Assert.True(result.UtilizationPercent > 0);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedRequirementAsync(AppDbContext dbContext)
    {
        var partId = Guid.NewGuid();
        var launchId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        dbContext.Parts.Add(new Part { Id = partId, Name = "Part" });
        dbContext.Sections.Add(new Section { Id = sectionId, Name = "Sec" });
        dbContext.WipLaunches.Add(new WipLaunch
        {
            Id = launchId,
            PartId = partId,
            SectionId = sectionId,
            UserId = Guid.NewGuid(),
            FromOpNumber = 10,
            LaunchDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 10m,
            SumHoursToFinish = 1m,
        });

        var requirementId = Guid.NewGuid();
        dbContext.MetalRequirements.Add(new MetalRequirement
        {
            Id = requirementId,
            RequirementNumber = "MREQ-000001",
            RequirementDate = DateTime.UtcNow,
            WipLaunchId = launchId,
            PartId = partId,
            Quantity = 10m,
            Status = "Создано",
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();
        return requirementId;
    }
}
