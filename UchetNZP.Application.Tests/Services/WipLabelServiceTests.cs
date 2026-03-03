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

    [Fact]
    public async Task MergeLabelsAsync_CreatesOutputAndMarksInputsMerged()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.WipLabels.AddRange(
            new WipLabel
            {
                Id = firstId,
                PartId = part.Id,
                Number = "900",
                LabelDate = DateTime.UtcNow,
                Quantity = 10m,
                RemainingQuantity = 4m,
                IsAssigned = true,
                Status = WipLabelStatus.Active,
                RootLabelId = firstId,
                RootNumber = "900",
                Suffix = 0,
            },
            new WipLabel
            {
                Id = secondId,
                PartId = part.Id,
                Number = "900/1",
                LabelDate = DateTime.UtcNow,
                Quantity = 10m,
                RemainingQuantity = 6m,
                IsAssigned = true,
                Status = WipLabelStatus.Active,
                RootLabelId = secondId,
                RootNumber = "900",
                Suffix = 1,
            });
        await dbContext.SaveChangesAsync();

        var service = new WipLabelService(dbContext);
        var result = await service.MergeLabelsAsync(new WipLabelMergeRequestDto(new[] { firstId, secondId }, DateTime.UtcNow, null));

        Assert.Equal(10m, result.Quantity);
        var output = await dbContext.WipLabels.SingleAsync(x => x.Id == result.OutputLabelId);
        Assert.Equal(10m, output.RemainingQuantity);

        var inputs = await dbContext.WipLabels.Where(x => x.Id == firstId || x.Id == secondId).ToListAsync();
        Assert.All(inputs, x =>
        {
            Assert.Equal(WipLabelStatus.Merged, x.Status);
            Assert.Equal(0m, x.RemainingQuantity);
        });

        Assert.Equal(2, await dbContext.LabelMerges.CountAsync());
    }

    [Fact]
    public async Task GetMergeTraceAsync_ReturnsForwardAndBackwardLinks()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        dbContext.Parts.Add(part);
        dbContext.WipLabels.AddRange(
            new WipLabel
            {
                Id = inputId,
                PartId = part.Id,
                Number = "901",
                LabelDate = now,
                Quantity = 5m,
                RemainingQuantity = 0m,
                IsAssigned = true,
                Status = WipLabelStatus.Merged,
                RootLabelId = inputId,
                RootNumber = "901",
                Suffix = 0,
            },
            new WipLabel
            {
                Id = outputId,
                PartId = part.Id,
                Number = "901/1",
                LabelDate = now,
                Quantity = 5m,
                RemainingQuantity = 5m,
                IsAssigned = true,
                Status = WipLabelStatus.Active,
                RootLabelId = outputId,
                RootNumber = "901",
                Suffix = 1,
            });
        dbContext.LabelMerges.Add(new LabelMerge { Id = Guid.NewGuid(), InputLabelId = inputId, OutputLabelId = outputId, CreatedAt = now });
        await dbContext.SaveChangesAsync();

        var service = new WipLabelService(dbContext);
        var inputTrace = await service.GetMergeTraceAsync(inputId);
        var outputTrace = await service.GetMergeTraceAsync(outputId);

        Assert.Single(inputTrace.ToLabels);
        Assert.Single(outputTrace.FromLabels);
        Assert.Equal(outputId, inputTrace.ToLabels.First().OutputLabelId);
        Assert.Equal(inputId, outputTrace.FromLabels.First().InputLabelId);
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
