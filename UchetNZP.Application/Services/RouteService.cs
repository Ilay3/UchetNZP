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

    public async Task<PartRoute> UpsertRouteAsync(
        string partName,
        string? partCode,
        string operationName,
        int opNumber,
        decimal normHours,
        string sectionName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partName))
        {
            throw new ArgumentException("Наименование детали обязательно.", nameof(partName));
        }

        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("Наименование операции обязательно.", nameof(operationName));
        }

        if (string.IsNullOrWhiteSpace(sectionName))
        {
            throw new ArgumentException("Наименование участка обязательно.", nameof(sectionName));
        }

        if (opNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(opNumber), opNumber, "Номер операции должен быть положительным.");
        }

        normHours = Math.Round(normHours, 3, MidpointRounding.AwayFromZero);

        if (normHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(normHours), normHours, "Норматив должен быть больше нуля.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var part = await ResolvePartAsync(partName, partCode, cancellationToken).ConfigureAwait(false);
            var operation = await ResolveOperationAsync(operationName, cancellationToken).ConfigureAwait(false);
            var section = await ResolveSectionAsync(sectionName, cancellationToken).ConfigureAwait(false);

            var route = await _dbContext.PartRoutes
                .FirstOrDefaultAsync(x => x.PartId == part.Id && x.OpNumber == opNumber, cancellationToken)
                .ConfigureAwait(false);

            if (route is null)
            {
                route = new PartRoute
                {
                    Id = Guid.NewGuid(),
                    PartId = part.Id,
                    OpNumber = opNumber,
                };

                await _dbContext.PartRoutes.AddAsync(route, cancellationToken).ConfigureAwait(false);
            }

            route.OperationId = operation.Id;
            route.SectionId = section.Id;
            route.NormHours = normHours;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return route;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<List<PartRoute>> LoadRoutes(Guid partId, CancellationToken cancellationToken)
    {
        return await _dbContext.PartRoutes
            .Where(x => x.PartId == partId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Part> ResolvePartAsync(string name, string? code, CancellationToken cancellationToken)
    {
        Part? entity;
        if (!string.IsNullOrWhiteSpace(code))
        {
            entity = await _dbContext.Parts.FirstOrDefaultAsync(x => x.Code == code, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity = await _dbContext.Parts.FirstOrDefaultAsync(x => x.Name == name, cancellationToken).ConfigureAwait(false);
        }

        if (entity is null)
        {
            entity = new Part
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim(),
            };

            await _dbContext.Parts.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity.Name = name.Trim();
            entity.Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        }

        return entity;
    }

    private async Task<Operation> ResolveOperationAsync(string name, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Operations.FirstOrDefaultAsync(x => x.Name == name, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            entity = new Operation
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
            };

            await _dbContext.Operations.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity.Name = name.Trim();
        }

        return entity;
    }

    private async Task<Section> ResolveSectionAsync(string name, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Sections.FirstOrDefaultAsync(x => x.Name == name, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            entity = new Section
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
            };

            await _dbContext.Sections.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity.Name = name.Trim();
        }

        return entity;
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
