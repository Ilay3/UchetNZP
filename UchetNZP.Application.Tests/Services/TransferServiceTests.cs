using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Transfers;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class TransferServiceTests
{
    [Fact]
    public async Task AddTransfersBatchAsync_WithoutScrap_UpdatesBalances()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Участок 10" };
        var toSection = new Section { Id = Guid.NewGuid(), Name = "Участок 20" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var toOperation = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, toSection);
        dbContext.Operations.AddRange(fromOperation, toOperation);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10),
            CreateRoute(part.Id, toSection.Id, toOperation.Id, 20));

        var fromBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 100m,
        };

        var toBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = toSection.Id,
            OpNumber = 20,
            Quantity = 15m,
        };

        dbContext.WipBalances.AddRange(fromBalance, toBalance);

        await dbContext.SaveChangesAsync();

        var routeService = new RouteService(dbContext);
        var currentUser = new TestCurrentUserService();
        var service = new TransferService(dbContext, routeService, currentUser);

        var transferDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 40m, "", null)
        });

        Assert.Equal(1, summary.Saved);
        var item = Assert.Single(summary.Items);
        Assert.Equal(60m, item.FromBalanceAfter);
        Assert.Equal(55m, item.ToBalanceAfter);
        Assert.Null(item.Scrap);

        var updatedFromBalance = await dbContext.WipBalances.SingleAsync(x => x.Id == fromBalance.Id);
        Assert.Equal(60m, updatedFromBalance.Quantity);

        var updatedToBalance = await dbContext.WipBalances.SingleAsync(x => x.Id == toBalance.Id);
        Assert.Equal(55m, updatedToBalance.Quantity);

        Assert.Empty(dbContext.WipScraps);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithScrap_ZeroesFromBalanceAndCreatesScrap()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Участок 10" };
        var toSection = new Section { Id = Guid.NewGuid(), Name = "Участок 20" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var toOperation = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, toSection);
        dbContext.Operations.AddRange(fromOperation, toOperation);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10),
            CreateRoute(part.Id, toSection.Id, toOperation.Id, 20));

        var fromBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 120m,
        };

        dbContext.WipBalances.Add(fromBalance);

        await dbContext.SaveChangesAsync();

        var routeService = new RouteService(dbContext);
        var currentUser = new TestCurrentUserService();
        var service = new TransferService(dbContext, routeService, currentUser);

        var transferDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comment = "Сломалось";

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(
                part.Id,
                10,
                20,
                transferDate,
                80m,
                null,
                new TransferScrapDto(ScrapType.Technological, 40m, comment))
        });

        Assert.Equal(1, summary.Saved);
        var item = Assert.Single(summary.Items);
        Assert.Equal(0m, item.FromBalanceAfter);
        Assert.NotNull(item.Scrap);
        Assert.Equal(40m, item.Scrap!.Quantity);
        Assert.Equal(ScrapType.Technological, item.Scrap.ScrapType);
        Assert.Equal(comment, item.Scrap.Comment);

        var updatedFromBalance = await dbContext.WipBalances.SingleAsync(x => x.Id == fromBalance.Id);
        Assert.Equal(0m, updatedFromBalance.Quantity);

        var scrap = await dbContext.WipScraps.Include(x => x.Transfer).SingleAsync();
        Assert.Equal(40m, scrap.Quantity);
        Assert.Equal(ScrapType.Technological, scrap.ScrapType);
        Assert.Equal(currentUser.UserId, scrap.UserId);
        Assert.Equal(comment, scrap.Comment);
        Assert.Equal(10, scrap.OpNumber);
        Assert.Equal(fromSection.Id, scrap.SectionId);
        Assert.Equal(part.Id, scrap.PartId);
        Assert.True(scrap.RecordedAt > DateTime.MinValue);
        Assert.NotNull(scrap.Transfer);
        Assert.Equal(item.TransferId, scrap.Transfer!.Id);
    }

    private static PartRoute CreateRoute(Guid partId, Guid sectionId, Guid operationId, int opNumber)
    {
        return new PartRoute
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OperationId = operationId,
            OpNumber = opNumber,
            NormHours = 1m,
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
        public Guid UserId { get; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }
}
