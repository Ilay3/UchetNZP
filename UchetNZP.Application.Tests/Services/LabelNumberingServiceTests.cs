using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class LabelNumberingServiceTests
{
    [Fact]
    public async Task GetNextSuffixAsync_ParallelRequestsForSameRoot_ReturnsUniqueSuffixes()
    {
        var databaseName = Guid.NewGuid().ToString();
        var root = "50001";
        const int parallelCount = 40;

        await using (var seedContext = CreateContext(databaseName))
        {
            var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
            seedContext.Parts.Add(part);
            seedContext.WipLabels.Add(new WipLabel
            {
                Id = Guid.NewGuid(),
                PartId = part.Id,
                LabelDate = DateTime.UtcNow,
                Quantity = 1m,
                RemainingQuantity = 1m,
                Number = root,
                RootLabelId = Guid.NewGuid(),
                RootNumber = root,
                Suffix = 0,
            });
            await seedContext.SaveChangesAsync();
        }

        var tasks = Enumerable.Range(0, parallelCount)
            .Select(async _ =>
            {
                await using var context = CreateContext(databaseName);
                var service = new LabelNumberingService(context);
                var suffix = await service.GetNextSuffixAsync(root);
                await context.SaveChangesAsync();
                return suffix;
            });

        var allocated = await Task.WhenAll(tasks);

        Assert.Equal(parallelCount, allocated.Distinct().Count());
        Assert.Equal(1, allocated.Min());
        Assert.Equal(parallelCount, allocated.Max());
    }

    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}
