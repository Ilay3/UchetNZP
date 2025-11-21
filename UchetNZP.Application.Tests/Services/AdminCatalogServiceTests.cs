using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Admin;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class AdminCatalogServiceTests
{
    [Fact]
    public async Task CreatePartAsync_PersistsEntity()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);

        var result = await service.CreatePartAsync(new AdminPartEditDto("Деталь 1", "P-001"));

        var part = await dbContext.Parts.SingleAsync();
        Assert.Equal(part.Id, result.Id);
        Assert.Equal("Деталь 1", part.Name);
        Assert.Equal("P-001", part.Code);
    }

    [Fact]
    public async Task UpdatePartAsync_ThrowsForUnknownId()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdatePartAsync(Guid.NewGuid(), new AdminPartEditDto("Name", null)));
    }

    [Fact]
    public async Task CreateWipBalanceAsync_RequiresExistingReferences()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateWipBalanceAsync(new AdminWipBalanceEditDto(Guid.NewGuid(), Guid.NewGuid(), 10, 5m)));
    }

    [Fact]
    public async Task UpdateWipBalanceAsync_UpdatesAllFields()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);

        var part1 = new Part { Id = Guid.NewGuid(), Name = "Деталь A" };
        var part2 = new Part { Id = Guid.NewGuid(), Name = "Деталь B" };
        var section1 = new Section { Id = Guid.NewGuid(), Name = "Участок 1" };
        var section2 = new Section { Id = Guid.NewGuid(), Name = "Участок 2" };

        dbContext.Parts.AddRange(part1, part2);
        dbContext.Sections.AddRange(section1, section2);

        var balance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part1.Id,
            SectionId = section1.Id,
            OpNumber = 5,
            Quantity = 10m,
        };

        dbContext.WipBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        var updated = await service.UpdateWipBalanceAsync(
            balance.Id,
            new AdminWipBalanceEditDto(part2.Id, section2.Id, 8, 22.5m));

        Assert.Equal(part2.Id, updated.PartId);
        Assert.Equal(section2.Id, updated.SectionId);
        Assert.Equal(8, updated.OpNumber);
        Assert.Equal(22.5m, updated.Quantity);

        var persisted = await dbContext.WipBalances.SingleAsync();
        Assert.Equal(part2.Id, persisted.PartId);
        Assert.Equal(section2.Id, persisted.SectionId);
        Assert.Equal(8, persisted.OpNumber);
        Assert.Equal(22.5m, persisted.Quantity);
    }

    [Fact]
    public async Task GetWipBalancesAsync_ReturnsBalancesWithRouteInfo()
    {
        await using var dbContext = CreateContext();
        var service = CreateService(dbContext);

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь X" };
        var section = new Section { Id = Guid.NewGuid(), Name = "Участок A" };
        var operation = new Operation { Id = Guid.NewGuid(), Name = "Фрезеровка", Code = "OP-10" };

        var routedBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = section.Id,
            OpNumber = 3,
            Quantity = 7m,
        };

        var orphanBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = section.Id,
            OpNumber = 5,
            Quantity = 4m,
        };

        var partRoute = new PartRoute
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = section.Id,
            OpNumber = 3,
            OperationId = operation.Id,
            Operation = operation,
        };

        dbContext.Parts.Add(part);
        dbContext.Sections.Add(section);
        dbContext.Operations.Add(operation);
        dbContext.PartRoutes.Add(partRoute);
        dbContext.WipBalances.AddRange(routedBalance, orphanBalance);
        await dbContext.SaveChangesAsync();

        var result = await service.GetWipBalancesAsync();

        Assert.Equal(2, result.Count);

        var routedDto = result.Single(x => x.OpNumber == routedBalance.OpNumber);
        Assert.Equal(operation.Id, routedDto.OperationId);
        Assert.Equal(operation.Name, routedDto.OperationName);
        Assert.Equal(operation.Code, routedDto.OperationLabel);

        var orphanDto = result.Single(x => x.OpNumber == orphanBalance.OpNumber);
        Assert.Null(orphanDto.OperationId);
        Assert.Equal(string.Empty, orphanDto.OperationName);
        Assert.Null(orphanDto.OperationLabel);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static IAdminCatalogService CreateService(AppDbContext dbContext)
        => new AdminCatalogService(dbContext);
}
