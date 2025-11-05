using System;
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
