using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Configuration;

public static class RouteOperationNameSynchronizer
{
    public static async Task EnsureOperationNamesMatchSectionsAsync(
        AppDbContext in_dbContext,
        CancellationToken in_cancellationToken)
    {
        if (in_dbContext is null)
        {
            throw new ArgumentNullException(nameof(in_dbContext));
        }

        var partRoutes = await in_dbContext.PartRoutes
            .Include(x => x.Operation)
            .Include(x => x.Section)
            .Where(x => x.Operation != null && x.Section != null)
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var operationsToUpdate = new Dictionary<Guid, Operation>();

        foreach (var route in partRoutes)
        {
            if (route.Operation is null || route.Section is null)
            {
                continue;
            }

            var targetName = route.Section.Name;
            if (string.Equals(route.Operation.Name, targetName, StringComparison.Ordinal))
            {
                continue;
            }

            route.Operation.Name = targetName;

            if (!operationsToUpdate.ContainsKey(route.Operation.Id))
            {
                operationsToUpdate[route.Operation.Id] = route.Operation;
            }
        }

        if (operationsToUpdate.Count > 0)
        {
            await in_dbContext.SaveChangesAsync(in_cancellationToken).ConfigureAwait(false);
        }
    }
}
