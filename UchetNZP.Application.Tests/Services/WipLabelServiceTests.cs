using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class WipLabelServiceTests
{
    [Fact]
    public async Task GetLabelStateAsync_ReturnsStatusAndCurrentPositionByLabelId()
    {
        await using var dbContext = CreateContext();

        var labelId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        dbContext.Parts.Add(part);
        dbContext.WipLabels.Add(new WipLabel
        {
            Id = labelId,
            PartId = part.Id,
            Number = "501/2",
            LabelDate = DateTime.UtcNow,
            Quantity = 10m,
            RemainingQuantity = 6m,
            IsAssigned = true,
            Status = WipLabelStatus.Active,
            CurrentSectionId = sectionId,
            CurrentOpNumber = 40,
            RootLabelId = labelId,
            ParentLabelId = Guid.NewGuid(),
            RootNumber = "501",
            Suffix = 2,
        });

        await dbContext.SaveChangesAsync();

        var service = new WipLabelService(dbContext);
        var state = await service.GetLabelStateAsync(labelId);

        Assert.Equal(labelId, state.Id);
        Assert.Equal("Active", state.Status);
        Assert.Equal(sectionId, state.CurrentSectionId);
        Assert.Equal(40, state.CurrentOpNumber);
        Assert.Equal("501", state.RootNumber);
        Assert.Equal(2, state.Suffix);
        Assert.Equal(6m, state.RemainingQuantity);
    }

    [Fact]
    public async Task CreateLabelWithNumberAsync_InitializesLineageFields()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        dbContext.Parts.Add(part);
        await dbContext.SaveChangesAsync();

        var service = new WipLabelService(dbContext);
        var created = await service.CreateLabelWithNumberAsync(new WipLabelManualCreateDto(part.Id, DateTime.UtcNow, 7m, "700/1"));

        var state = await service.GetLabelStateAsync(created.Id);

        Assert.Equal(created.Id, state.RootLabelId);
        Assert.Null(state.ParentLabelId);
        Assert.Equal("00700", state.RootNumber);
        Assert.Equal(1, state.Suffix);
        Assert.Equal("Active", state.Status);
        Assert.Null(state.CurrentSectionId);
        Assert.Null(state.CurrentOpNumber);
    }

    [Fact]
    public async Task GetLabelLedgerAsync_ReturnsChronologyForLabel()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var labelId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.WipLabels.Add(new WipLabel
        {
            Id = labelId,
            PartId = part.Id,
            Number = "900",
            LabelDate = DateTime.UtcNow,
            Quantity = 10m,
            RemainingQuantity = 8m,
            IsAssigned = true,
            Status = WipLabelStatus.Active,
            RootLabelId = labelId,
            RootNumber = "900",
            Suffix = 0,
        });

        dbContext.WipLabelLedger.AddRange(
            new WipLabelLedger
            {
                EventId = Guid.NewGuid(),
                EventTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                UserId = userId,
                TransactionId = transactionId,
                EventType = WipLabelEventType.Receipt,
                FromLabelId = labelId,
                ToLabelId = labelId,
                Qty = 10m,
                RefEntityType = "WipReceipt",
            },
            new WipLabelLedger
            {
                EventId = Guid.NewGuid(),
                EventTime = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
                UserId = userId,
                TransactionId = transactionId,
                EventType = WipLabelEventType.Transfer,
                FromLabelId = labelId,
                ToLabelId = labelId,
                Qty = 2m,
                RefEntityType = "WipTransfer",
            });

        await dbContext.SaveChangesAsync();

        var service = new WipLabelService(dbContext);
        var timeline = await service.GetLabelLedgerAsync(labelId);

        Assert.Equal(2, timeline.Count);
        Assert.Collection(
            timeline,
            first => Assert.Equal("Receipt", first.EventType),
            second => Assert.Equal("Transfer", second.EventType));
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
