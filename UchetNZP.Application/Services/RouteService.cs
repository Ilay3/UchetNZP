using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class RouteService : IRouteService
{
    private const int NormalizedOpNumberLength = 10;

    private readonly AppDbContext _dbContext;

    public RouteService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<PartRoute>> GetRouteAsync(Guid partId, CancellationToken cancellationToken = default)
    {
        var routes = await LoadRoutes(partId, cancellationToken).ConfigureAwait(false);

        return routes
            .OrderBy(x => Normalize(x.OpNumber), StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<PartRoute>> GetTailToFinishAsync(Guid partId, string fromOpNumber, CancellationToken cancellationToken = default)
    {
        if (fromOpNumber is null)
        {
            throw new ArgumentNullException(nameof(fromOpNumber));
        }

        var normalizedFromOpNumber = Normalize(fromOpNumber);

        var routes = await LoadRoutes(partId, cancellationToken).ConfigureAwait(false);

        return routes
            .Where(x => string.CompareOrdinal(Normalize(x.OpNumber), normalizedFromOpNumber) >= 0)
            .OrderBy(x => Normalize(x.OpNumber), StringComparer.Ordinal)
            .ToList();
    }

    private async Task<List<PartRoute>> LoadRoutes(Guid partId, CancellationToken cancellationToken)
    {
        return await _dbContext.PartRoutes
            .Where(x => x.PartId == partId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string Normalize(int opNumber)
    {
        return opNumber.ToString($"D{NormalizedOpNumberLength}", CultureInfo.InvariantCulture);
    }

    private static string Normalize(string opNumber)
    {
        var trimmed = opNumber.Trim();

        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return trimmed.All(char.IsDigit) && trimmed.Length < NormalizedOpNumberLength
            ? trimmed.PadLeft(NormalizedOpNumberLength, '0')
            : trimmed;
    }
}
