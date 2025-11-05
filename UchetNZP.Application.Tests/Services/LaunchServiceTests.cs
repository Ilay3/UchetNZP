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
        var launchService = new LaunchService(dbContext, routeService, new TestCurrentUserService());

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

        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService());

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
        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService());

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

        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteLaunchAsync(launchId));
        Assert.Contains("Остаток НЗП", exception.Message);
    }

    [Fact]
    public async Task DeleteLaunchAsync_ThrowsForEmptyId()
    {
        await using var dbContext = CreateContext();
        var service = new LaunchService(dbContext, new RouteService(dbContext), new TestCurrentUserService());

        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteLaunchAsync(Guid.Empty));
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
