using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        var section = new Section { Id = sectionId, Name = "Участок" };
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
        var launchService = new LaunchService(dbContext, routeService);

        var launchDate = new DateTime(2025, 1, 1);
        var summary = await launchService.AddLaunchesBatchAsync(
            new[] { new LaunchItemDto(partId, 15, launchDate, 40m, "Партия-1") });

        // Assert
        Assert.Equal(1, summary.Saved);
        var item = Assert.Single(summary.Items);
        Assert.Equal(12.4m, item.SumHoursToFinish);
        Assert.Equal(60m, item.Remaining);

        var balance = await dbContext.WipBalances.FirstAsync(x => x.PartId == partId && x.OpNumber == 15);
        Assert.Equal(60m, balance.Quantity);

        var launch = await dbContext.WipLaunches
            .Include(x => x.Operations)
            .ThenInclude(x => x.Operation)
            .SingleAsync();

        Assert.Equal(4, launch.Operations.Count);
        Assert.Equal(12.4m, launch.Operations.Sum(x => x.Hours));
        Assert.All(launch.Operations, operation => Assert.Equal(40m, operation.Quantity));
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
}
