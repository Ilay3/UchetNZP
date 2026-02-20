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
    public async Task BulkCleanup_PreviewAndExecute_ZeroesOnlyFilteredBalancesAndWritesAudit()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        var targetBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 65,
            Quantity = 120m,
        };

        var untouchedBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 70,
            Quantity = 80m,
        };

        dbContext.WipBalances.AddRange(targetBalance, untouchedBalance);
        await dbContext.SaveChangesAsync();

        var service = new AdminWipService(dbContext, new TestCurrentUserService());

        var preview = await service.PreviewBulkCleanupAsync(new AdminWipBulkCleanupRequestDto(partId, sectionId, 65, 1m, "годовая выборочная очистка"));

        Assert.Equal(1, preview.AffectedCount);
        Assert.Equal(120m, preview.AffectedQuantity);

        var result = await service.ExecuteBulkCleanupAsync(new AdminWipBulkCleanupExecuteDto(preview.JobId, true));

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(120m, result.UpdatedQuantity);

        var refreshedTarget = await dbContext.WipBalances.SingleAsync(x => x.Id == targetBalance.Id);
        var refreshedUntouched = await dbContext.WipBalances.SingleAsync(x => x.Id == untouchedBalance.Id);
        Assert.Equal(0m, refreshedTarget.Quantity);
        Assert.Equal(80m, refreshedUntouched.Quantity);

        var cleanupJob = await dbContext.WipBalanceCleanupJobs.SingleAsync(x => x.Id == preview.JobId);
        Assert.True(cleanupJob.IsExecuted);

        var adjustment = await dbContext.WipBalanceAdjustments.SingleAsync(x => x.WipBalanceId == targetBalance.Id);
        Assert.StartsWith("BULK-CLEANUP:", adjustment.Comment);
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
