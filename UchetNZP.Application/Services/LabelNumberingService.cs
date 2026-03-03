using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class LabelNumberingService : ILabelNumberingService
{
    private const int MaxRetries = 5;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InMemoryLocks = new(StringComparer.Ordinal);
    private readonly AppDbContext m_dbContext;

    public LabelNumberingService(AppDbContext in_dbContext)
    {
        m_dbContext = in_dbContext ?? throw new ArgumentNullException(nameof(in_dbContext));
    }

    public async Task<int> GetNextSuffixAsync(string in_rootNumber, CancellationToken in_cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(in_rootNumber))
        {
            throw new InvalidOperationException("Базовый номер ярлыка не может быть пустым.");
        }

        var normalizedRoot = in_rootNumber.Trim();

        if (!m_dbContext.Database.IsRelational())
        {
            var sync = InMemoryLocks.GetOrAdd(normalizedRoot, _ => new SemaphoreSlim(1, 1));
            await sync.WaitAsync(in_cancellationToken).ConfigureAwait(false);
            try
            {
                var current = m_dbContext.LabelNumberCounters.Local
                    .FirstOrDefault(x => x.RootNumber == normalizedRoot);

                if (current is null)
                {
                    current = await m_dbContext.LabelNumberCounters
                        .FirstOrDefaultAsync(x => x.RootNumber == normalizedRoot, in_cancellationToken)
                        .ConfigureAwait(false);
                }

                if (current is null)
                {
                    var existingSuffixes = await m_dbContext.WipLabels
                        .AsNoTracking()
                        .Where(x => x.RootNumber == normalizedRoot || x.Number == normalizedRoot || x.Number.StartsWith($"{normalizedRoot}/"))
                        .Select(x => string.Equals(x.RootNumber, normalizedRoot, StringComparison.Ordinal)
                            ? x.Suffix
                            : WipLabelInvariants.ParseNumber(x.Number).Suffix)
                        .ToListAsync(in_cancellationToken)
                        .ConfigureAwait(false);

                    var next = existingSuffixes.Count == 0 ? 1 : existingSuffixes.Max() + 1;
                    var counter = new LabelNumberCounter
                    {
                        RootNumber = normalizedRoot,
                        NextSuffix = next + 1,
                    };

                    await m_dbContext.LabelNumberCounters.AddAsync(counter, in_cancellationToken).ConfigureAwait(false);
                    return next;
                }

                var reserved = current.NextSuffix;
                current.NextSuffix = current.NextSuffix + 1;
                return reserved;
            }
            finally
            {
                sync.Release();
            }
        }

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var rows = await m_dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"
                INSERT INTO ""LabelNumberCounters"" (""RootNumber"", ""NextSuffix"")
                VALUES ({normalizedRoot}, 2)
                ON CONFLICT (""RootNumber"")
                DO UPDATE SET ""NextSuffix"" = ""LabelNumberCounters"".""NextSuffix"" + 1;",
                in_cancellationToken)
                .ConfigureAwait(false);

            if (rows <= 0)
            {
                continue;
            }

            var reserved = await m_dbContext.LabelNumberCounters
                .AsNoTracking()
                .Where(x => x.RootNumber == normalizedRoot)
                .Select(x => x.NextSuffix - 1)
                .FirstAsync(in_cancellationToken)
                .ConfigureAwait(false);

            var alreadyUsed = await m_dbContext.WipLabels
                .AsNoTracking()
                .AnyAsync(x => x.RootNumber == normalizedRoot && x.Suffix == reserved, in_cancellationToken)
                .ConfigureAwait(false);

            if (!alreadyUsed)
            {
                return reserved;
            }
        }

        throw new InvalidOperationException($"Не удалось выделить следующий суффикс для базового номера {normalizedRoot}.");
    }
}
