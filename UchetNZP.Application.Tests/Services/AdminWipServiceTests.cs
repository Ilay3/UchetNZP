using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Admin;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class AdminWipServiceTests
{
    [Fact]
    public async Task AdjustBalanceAsync_UpdatesQuantityAndCreatesLog()
    {
        await using var dbContext = CreateContext();
        var balanceId = Guid.NewGuid();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = balanceId,
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 10,
            Quantity = 15.5m,
        });

        await dbContext.SaveChangesAsync();

        var service = new AdminWipService(dbContext, new TestCurrentUserService());

        var result = await service.AdjustBalanceAsync(new AdminWipAdjustmentRequestDto(balanceId, 20m, "Корректировка"));

        Assert.Equal(balanceId, result.BalanceId);
        Assert.Equal(15.5m, result.PreviousQuantity);
        Assert.Equal(20m, result.NewQuantity);
        Assert.Equal(4.5m, result.Delta);
        Assert.NotEqual(Guid.Empty, result.AdjustmentId);

        var balance = await dbContext.WipBalances.SingleAsync();
        Assert.Equal(20m, balance.Quantity);

        var log = await dbContext.WipBalanceAdjustments.SingleAsync();
        Assert.Equal(balanceId, log.WipBalanceId);
        Assert.Equal(4.5m, log.Delta);
        Assert.Equal("Корректировка", log.Comment);
    }

    [Fact]
    public async Task AdjustBalanceAsync_DoesNothingWhenQuantityUnchanged()
    {
        await using var dbContext = CreateContext();
        var balanceId = Guid.NewGuid();

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = balanceId,
            PartId = Guid.NewGuid(),
            SectionId = Guid.NewGuid(),
            OpNumber = 20,
            Quantity = 5m,
        });

        await dbContext.SaveChangesAsync();

        var service = new AdminWipService(dbContext, new TestCurrentUserService());

        var result = await service.AdjustBalanceAsync(new AdminWipAdjustmentRequestDto(balanceId, 5m, null));

        Assert.Equal(balanceId, result.BalanceId);
        Assert.Equal(0m, result.Delta);
        Assert.Equal(Guid.Empty, result.AdjustmentId);
        Assert.False(await dbContext.WipBalanceAdjustments.AnyAsync());
    }

    [Fact]
    public async Task AdjustBalanceAsync_ThrowsForNegativeQuantity()
    {
        await using var dbContext = CreateContext();
        var service = new AdminWipService(dbContext, new TestCurrentUserService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdjustBalanceAsync(new AdminWipAdjustmentRequestDto(Guid.NewGuid(), -1m, null)));
    }

    [Fact]
    public async Task ForceDeleteLabelAsync_RemovesLabelAndCleansReferences()
    {
        await using var dbContext = CreateContext();

        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        var childLabelId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var warehouseItemId = Guid.NewGuid();

        dbContext.Parts.Add(new Part
        {
            Id = partId,
            Name = "Деталь",
        });

        dbContext.Sections.Add(new Section
        {
            Id = sectionId,
            Name = "Участок",
        });

        dbContext.WipLabels.AddRange(
            new WipLabel
            {
                Id = labelId,
                PartId = partId,
                Number = "500",
                LabelDate = DateTime.UtcNow,
                Quantity = 10m,
                RemainingQuantity = 3m,
                IsAssigned = true,
                Status = WipLabelStatus.Active,
                CurrentSectionId = sectionId,
                CurrentOpNumber = 10,
                RootLabelId = labelId,
                RootNumber = "500",
                Suffix = 0,
            },
            new WipLabel
            {
                Id = childLabelId,
                PartId = partId,
                Number = "500/1",
                LabelDate = DateTime.UtcNow,
                Quantity = 4m,
                RemainingQuantity = 4m,
                IsAssigned = true,
                Status = WipLabelStatus.Active,
                CurrentSectionId = sectionId,
                CurrentOpNumber = 20,
                RootLabelId = labelId,
                ParentLabelId = labelId,
                RootNumber = "500",
                Suffix = 1,
            });

        dbContext.WipReceipts.Add(new WipReceipt
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 10,
            UserId = Guid.NewGuid(),
            ReceiptDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 10m,
            WipLabelId = labelId,
        });

        dbContext.WipTransfers.Add(new WipTransfer
        {
            Id = transferId,
            PartId = partId,
            FromSectionId = sectionId,
            FromOpNumber = 10,
            ToSectionId = sectionId,
            ToOpNumber = 20,
            UserId = Guid.NewGuid(),
            TransferDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 2m,
            WipLabelId = labelId,
        });

        dbContext.TransferAudits.Add(new TransferAudit
        {
            Id = Guid.NewGuid(),
            TransactionId = Guid.NewGuid(),
            TransferId = transferId,
            PartId = partId,
            FromSectionId = sectionId,
            FromOpNumber = 10,
            ToSectionId = sectionId,
            ToOpNumber = 20,
            Quantity = 2m,
            TransferDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UserId = Guid.NewGuid(),
            FromBalanceBefore = 10m,
            FromBalanceAfter = 8m,
            ToBalanceBefore = 0m,
            ToBalanceAfter = 2m,
            WipLabelId = labelId,
            ResidualWipLabelId = labelId,
        });

        dbContext.TransferLabelUsages.Add(new TransferLabelUsage
        {
            Id = Guid.NewGuid(),
            TransferId = transferId,
            FromLabelId = labelId,
            CreatedToLabelId = labelId,
            Qty = 1m,
            ScrapQty = 0m,
            RemainingBefore = 3m,
        });

        dbContext.LabelMerges.Add(new LabelMerge
        {
            Id = Guid.NewGuid(),
            InputLabelId = labelId,
            OutputLabelId = childLabelId,
            CreatedAt = DateTime.UtcNow,
        });

        dbContext.WarehouseItems.Add(new WarehouseItem
        {
            Id = warehouseItemId,
            PartId = partId,
            Quantity = 1m,
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });

        dbContext.WarehouseLabelItems.Add(new WarehouseLabelItem
        {
            Id = Guid.NewGuid(),
            WarehouseItemId = warehouseItemId,
            WipLabelId = labelId,
            Quantity = 1m,
            AddedAt = DateTime.UtcNow,
        });

        dbContext.WipLabelLedger.Add(new WipLabelLedger
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTime.UtcNow,
            UserId = Guid.NewGuid(),
            TransactionId = Guid.NewGuid(),
            EventType = WipLabelEventType.Transfer,
            FromLabelId = labelId,
            ToLabelId = childLabelId,
            Qty = 1m,
            ScrapQty = 0m,
            RefEntityType = nameof(WipTransfer),
            RefEntityId = transferId,
        });

        await dbContext.SaveChangesAsync();

        var service = new AdminWipService(dbContext, new TestCurrentUserService());

        var deletedNumber = await service.ForceDeleteLabelAsync(labelId);

        Assert.Equal("500", deletedNumber);
        Assert.False(await dbContext.WipLabels.AnyAsync(x => x.Id == labelId));
        Assert.True(await dbContext.WipReceipts.AllAsync(x => x.WipLabelId == null));
        Assert.True(await dbContext.WipTransfers.AllAsync(x => x.WipLabelId == null));
        Assert.True(await dbContext.TransferAudits.AllAsync(x => x.WipLabelId == null && x.ResidualWipLabelId == null));
        Assert.False(await dbContext.TransferLabelUsages.AnyAsync());
        Assert.False(await dbContext.LabelMerges.AnyAsync());
        Assert.False(await dbContext.WarehouseLabelItems.AnyAsync());
        Assert.False(await dbContext.WipLabelLedger.AnyAsync());

        var childLabel = await dbContext.WipLabels.SingleAsync(x => x.Id == childLabelId);
        Assert.Null(childLabel.ParentLabelId);
        Assert.Equal(childLabel.Id, childLabel.RootLabelId);
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
