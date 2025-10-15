using System.Linq;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class RouteServiceTests
{
    [Fact]
    public async Task GetRoute_ReturnsOperationsOrderedLexicographically()
    {
        // Arrange
        var partId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        dbContext.PartRoutes.AddRange(
            CreateRoute(partId, 20),
            CreateRoute(partId, 10),
            CreateRoute(partId, 15),
            CreateRoute(Guid.NewGuid(), 5));
        await dbContext.SaveChangesAsync();

        var service = new RouteService(dbContext);

        // Act
        var orderedRoutes = await service.GetRouteAsync(partId);

        // Assert
        var opNumbers = orderedRoutes.Select(x => x.OpNumber.ToString("D3")).ToArray();
        Assert.Equal(new[] { "010", "015", "020" }, opNumbers);
    }

    [Fact]
    public async Task GetTailToFinish_ReturnsTailStartingFromProvidedOperation()
    {
        // Arrange
        var partId = Guid.NewGuid();
        await using var dbContext = CreateContext();
        dbContext.PartRoutes.AddRange(
            CreateRoute(partId, 5),
            CreateRoute(partId, 10),
            CreateRoute(partId, 15),
            CreateRoute(partId, 20),
            CreateRoute(Guid.NewGuid(), 30));
        await dbContext.SaveChangesAsync();

        var service = new RouteService(dbContext);

        // Act
        var tail = await service.GetTailToFinishAsync(partId, "015");

        // Assert
        var opNumbers = tail.Select(x => x.OpNumber.ToString("D3")).ToArray();
        Assert.Equal(new[] { "015", "020" }, opNumbers);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static PartRoute CreateRoute(Guid partId, int opNumber)
    {
        return new PartRoute
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            OpNumber = opNumber,
            OperationId = Guid.NewGuid(),
            SectionId = Guid.NewGuid(),
        };
    }
}
