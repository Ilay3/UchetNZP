using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Launches;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class LaunchServiceTests
{
    [Fact]
    public async Task AddLaunchesBatchAsync_ComputesSumHoursToFinish()
    {
        // Arrange
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        var part = new Part { Id = partId, Name = "Деталь" };
        var section = new Section { Id = sectionId, Name = "Вид работ" };
        var operations = new[]
        {
            new Operation { Id = Guid.NewGuid(), Name = "015" },
            new Operation { Id = Guid.NewGuid(), Name = "030" },
            new Operation { Id = Guid.NewGuid(), Name = "035" },
            new Operation { Id = Guid.NewGuid(), Name = "045" },
        };

        dbContext.Parts.Add(part);
        dbContext.Sections.Add(section);
        dbContext.Operations.AddRange(operations);
        dbContext.PartRoutes.AddRange(
            CreateRoute(partId, sectionId, operations[0].Id, 15, 0.112m),
            CreateRoute(partId, sectionId, operations[1].Id, 30, 0.087m),
            CreateRoute(partId, sectionId, operations[2].Id, 35, 0.040m),
            CreateRoute(partId, sectionId, operations[3].Id, 45, 0.071m));

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 15,
            Quantity = 100m,
        });

        await dbContext.SaveChangesAsync();

        var routeService = new RouteService(dbContext);
        var launchService = new LaunchService(dbContext, routeService, new TestCurrentUserService(), new MaterialSelectionService());

        var launchDate = new DateTime(2025, 1, 1);
        var summary = await launchService.AddLaunchesBatchAsync(
            new[] { new LaunchItemDto(partId, 15, launchDate, 40m, null) });

        // Assert
        Assert.Equal(1, summary.Saved);
        var item = Assert.Single(summary.Items);
        Assert.Equal(12.4m, item.SumHoursToFinish);
        Assert.Equal(100m, item.Remaining);

        var balance = await dbContext.WipBalances.FirstAsync(x => x.PartId == partId && x.OpNumber == 15);
        Assert.Equal(100m, balance.Quantity);

        var launch = await dbContext.WipLaunches
            .Include(x => x.Operations)
            .ThenInclude(x => x.Operation)
            .SingleAsync();

        Assert.Equal(4, launch.Operations.Count);
        Assert.Equal(12.4m, launch.Operations.Sum(x => x.Hours));
        Assert.All(launch.Operations, operation => Assert.Equal(40m, operation.Quantity));
    }

    [Fact]
    public async Task DeleteLaunchAsync_RemovesLaunchAndRestoresBalance()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var launchId = Guid.NewGuid();

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 15,
            Quantity = 10m,
        });

        var launch = new WipLaunch
        {
            Id = launchId,
            UserId = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            FromOpNumber = 15,
            LaunchDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 25m,
            Comment = "test",
            SumHoursToFinish = 7.5m,
        };

        dbContext.WipLaunches.Add(launch);
        dbContext.WipLaunchOperations.AddRange(
            new WipLaunchOperation
            {
                Id = Guid.NewGuid(),
                WipLaunchId = launchId,
                OperationId = Guid.NewGuid(),
                SectionId = sectionId,
                OpNumber = 20,
                Quantity = 25m,
                Hours = 5m,
                NormHours = 0.2m,
            },
            new WipLaunchOperation
            {
                Id = Guid.NewGuid(),
                WipLaunchId = launchId,
                OperationId = Guid.NewGuid(),
                SectionId = sectionId,
                OpNumber = 25,
                Quantity = 25m,
                Hours = 2.5m,
                NormHours = 0.1m,
            });

        await dbContext.SaveChangesAsync();

        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new MaterialSelectionService());

        var result = await service.DeleteLaunchAsync(launchId);

        Assert.Equal(launchId, result.LaunchId);
        Assert.Equal(10m, result.Remaining);

        Assert.False(await dbContext.WipLaunches.AnyAsync());
        Assert.False(await dbContext.WipLaunchOperations.AnyAsync());

        var balance = await dbContext.WipBalances.SingleAsync();
        Assert.Equal(10m, balance.Quantity);
    }

    [Fact]
    public async Task DeleteLaunchAsync_ThrowsForUnknownLaunch()
    {
        await using var dbContext = CreateContext();
        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new MaterialSelectionService());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteLaunchAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteLaunchAsync_ThrowsWhenBalanceMissing()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var launchId = Guid.NewGuid();

        dbContext.WipLaunches.Add(new WipLaunch
        {
            Id = launchId,
            UserId = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            FromOpNumber = 10,
            LaunchDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 5m,
            SumHoursToFinish = 1m,
        });

        await dbContext.SaveChangesAsync();

        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new MaterialSelectionService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteLaunchAsync(launchId));
        Assert.Contains("Остаток НЗП", exception.Message);
    }

    [Fact]
    public async Task DeleteLaunchAsync_ThrowsForEmptyId()
    {
        await using var dbContext = CreateContext();
        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new MaterialSelectionService());

        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteLaunchAsync(Guid.Empty));
    }


    [Fact]
    public async Task AddLaunchesBatchAsync_CreatesRequirementWithResolvedMaterial()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        await SeedBaseLaunchDataAsync(dbContext, partId, sectionId, operationId, "Деталь resolved");

        dbContext.MetalMaterials.Add(new MetalMaterial
        {
            Id = materialId,
            Name = "Круг 45",
            Code = "KRUG45",
            IsActive = true,
            UnitKind = "Meter",
            MassPerMeterKg = 1m,
            WeightPerUnitKg = 1m,
            CoefConsumption = 1m,
            Coefficient = 1m,
            StockUnit = "m",
            DisplayName = "Круг 45",
        });

        dbContext.MetalConsumptionNorms.Add(new MetalConsumptionNorm
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            MetalMaterialId = materialId,
            BaseConsumptionQty = 2m,
            ConsumptionUnit = "м",
            NormalizedConsumptionUnit = "m",
            NormalizedSizeRaw = string.Empty,
            NormKeyHash = Guid.NewGuid().ToString("N"),
            ShapeType = "rod",
            UnitNorm = "pcs",
            ParseStatus = "ok",
            IsActive = true,
        });

        await dbContext.SaveChangesAsync();

        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new MaterialSelectionService());
        await service.AddLaunchesBatchAsync(new[] { new LaunchItemDto(partId, 15, DateTime.UtcNow, 3m, null) });

        var requirement = await dbContext.MetalRequirements.Include(x => x.Items).SingleAsync();
        var item = Assert.Single(requirement.Items);

        Assert.Equal("Created", requirement.Status);
        Assert.Equal("Resolved", requirement.SelectionStatus);
        Assert.Equal(materialId, requirement.MetalMaterialId);
        Assert.Equal(materialId, item.MetalMaterialId);
        Assert.Equal(6m, item.TotalRequiredQty);
    }

    [Fact]
    public async Task AddLaunchesBatchAsync_CreatesDraftRequirementWhenMaterialIsNotResolved()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        await SeedBaseLaunchDataAsync(dbContext, partId, sectionId, operationId, "Неизвестная деталь");

        dbContext.MetalConsumptionNorms.Add(new MetalConsumptionNorm
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            MetalMaterialId = null,
            BaseConsumptionQty = 1.5m,
            ConsumptionUnit = "м",
            NormalizedConsumptionUnit = "m",
            NormalizedSizeRaw = string.Empty,
            NormKeyHash = Guid.NewGuid().ToString("N"),
            ShapeType = "unknown",
            UnitNorm = "pcs",
            ParseStatus = "ok",
            IsActive = true,
        });

        await dbContext.SaveChangesAsync();

        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new MaterialSelectionService());
        await service.AddLaunchesBatchAsync(new[] { new LaunchItemDto(partId, 15, DateTime.UtcNow, 2m, null) });

        var requirement = await dbContext.MetalRequirements.Include(x => x.Items).SingleAsync();
        var item = Assert.Single(requirement.Items);

        Assert.Equal("Draft", requirement.Status);
        Assert.Equal("NeedMaterialSelection", requirement.SelectionStatus);
        Assert.Null(requirement.MetalMaterialId);
        Assert.Null(item.MetalMaterialId);
    }

    [Fact]
    public async Task UpsertElectronicMetalRequirementAsync_RebuildsExistingRequirementInsteadOfCreatingDuplicate()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        await SeedBaseLaunchDataAsync(dbContext, partId, sectionId, operationId, "Деталь для update");

        dbContext.MetalMaterials.Add(new MetalMaterial
        {
            Id = materialId,
            Name = "Лист 09Г2С t=4",
            Code = "LIST09G2S4",
            IsActive = true,
            UnitKind = "SquareMeter",
            MassPerSquareMeterKg = 1m,
            WeightPerUnitKg = 1m,
            CoefConsumption = 1m,
            Coefficient = 1m,
            StockUnit = "m2",
            DisplayName = "Лист 09Г2С t=4",
        });

        dbContext.MetalConsumptionNorms.Add(new MetalConsumptionNorm
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            MetalMaterialId = materialId,
            BaseConsumptionQty = 1m,
            ConsumptionUnit = "м2",
            NormalizedConsumptionUnit = "m2",
            NormalizedSizeRaw = string.Empty,
            NormKeyHash = Guid.NewGuid().ToString("N"),
            ShapeType = "sheet",
            UnitNorm = "pcs",
            ParseStatus = "ok",
            IsActive = true,
        });

        var launch = new WipLaunch
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            FromOpNumber = 15,
            LaunchDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 2m,
            SumHoursToFinish = 1m,
        };

        dbContext.WipLaunches.Add(launch);
        await dbContext.SaveChangesAsync();

        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new MaterialSelectionService());
        var method = typeof(LaunchService)
            .GetMethod("UpsertElectronicMetalRequirementAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var firstTask = (Task)method!.Invoke(service, new object[] { launch, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), CancellationToken.None })!;
        await firstTask;
        await dbContext.SaveChangesAsync();

        launch.Quantity = 5m;
        var secondTask = (Task)method.Invoke(service, new object[] { launch, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), CancellationToken.None })!;
        await secondTask;
        await dbContext.SaveChangesAsync();

        Assert.Equal(1, await dbContext.MetalRequirements.CountAsync());
        var requirement = await dbContext.MetalRequirements.Include(x => x.Items).SingleAsync();
        Assert.Equal("Updated", requirement.Status);
        Assert.Single(requirement.Items);
        Assert.Equal(5m, requirement.Quantity);
    }

    private static async Task SeedBaseLaunchDataAsync(AppDbContext dbContext, Guid partId, Guid sectionId, Guid operationId, string partName)
    {
        dbContext.Parts.Add(new Part { Id = partId, Name = partName, Code = partName[..Math.Min(partName.Length, 8)] });
        dbContext.Sections.Add(new Section { Id = sectionId, Name = "Цех" });
        dbContext.Operations.Add(new Operation { Id = operationId, Name = "015" });
        dbContext.PartRoutes.Add(CreateRoute(partId, sectionId, operationId, 15, 0.5m));
        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 15,
            Quantity = 100m,
        });

        await dbContext.SaveChangesAsync();
    }

    private static PartRoute CreateRoute(Guid partId, Guid sectionId, Guid operationId, int opNumber, decimal norm)
    {
        return new PartRoute
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OperationId = operationId,
            OpNumber = opNumber,
            NormHours = norm,
        };
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    }
}
