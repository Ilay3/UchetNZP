using Microsoft.EntityFrameworkCore;

namespace UchetNZP.Infrastructure.Data;

public static class WarehouseCleanupExtensions
{
    public static async Task<int> CleanupWarehouseAsync(
        this AppDbContext dbContext,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var currentDayUtc = utcNow.Date;

        var obsoleteItems = await dbContext.WarehouseItems
            .Where(x => x.AddedAt < currentDayUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (obsoleteItems.Count == 0)
        {
            return 0;
        }

        dbContext.WarehouseItems.RemoveRange(obsoleteItems);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return obsoleteItems.Count;
    }
}
