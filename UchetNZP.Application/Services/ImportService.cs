using System.Globalization;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Imports;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class ImportService : IImportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;

    public ImportService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ImportSummaryDto> ImportRoutesExcelAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var headerRow = worksheet.FirstRowUsed() ?? throw new InvalidOperationException("Пустой файл Excel.");

        var headers = headerRow.CellsUsed()
            .ToDictionary(
                cell => cell.GetString().Trim(),
                cell => cell.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);

        int Require(string name)
        {
            if (!headers.TryGetValue(name, out var column))
            {
                throw new InvalidOperationException($"Не найден столбец '{name}'.");
            }

            return column;
        }

        var colPartName = Require("Наименование детали");
        var colPartCode = headers.TryGetValue("Обозначение", out var tmpPartCode) ? tmpPartCode : -1;
        var colOperationName = Require("Наименование операции");
        var colOpNumber = Require("№ операции");
        var colNorm = Require("Утвержденный норматив (н/ч)");
        var colSection = Require("Участок");

        var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1).ToList() ?? new List<IXLRangeRow>();

        var now = DateTime.UtcNow;
        var job = new ImportJob
        {
            Id = Guid.NewGuid(),
            Type = "RoutesExcel",
            Status = "InProgress",
            CreatedAt = now,
            StartedAt = now,
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _dbContext.ImportJobs.AddAsync(job, cancellationToken).ConfigureAwait(false);

            var partCache = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);
            var sectionCache = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
            var operationCache = new Dictionary<string, Operation>(StringComparer.OrdinalIgnoreCase);
            var routeCache = new Dictionary<string, PartRoute>(StringComparer.OrdinalIgnoreCase);

            var results = new List<ImportItemResultDto>();
            var saved = 0;
            var skipped = 0;

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rowNumber = row.RowNumber();

                string GetString(int column)
                {
                    return column > 0 ? row.Cell(column).GetString().Trim() : string.Empty;
                }

                var partName = GetString(colPartName);
                var partCode = GetString(colPartCode);
                var operationName = GetString(colOperationName);
                var opNumberText = GetString(colOpNumber);
                var normText = GetString(colNorm);
                var sectionName = GetString(colSection);

                var payload = JsonSerializer.Serialize(new
                {
                    partName,
                    partCode,
                    operationName,
                    opNumber = opNumberText,
                    norm = normText,
                    sectionName,
                }, SerializerOptions);

                if (string.IsNullOrWhiteSpace(partName) || string.IsNullOrWhiteSpace(operationName) || string.IsNullOrWhiteSpace(opNumberText) || string.IsNullOrWhiteSpace(normText) || string.IsNullOrWhiteSpace(sectionName))
                {
                    const string reason = "Пропущены обязательные поля.";
                    await AddJobItemAsync(job.Id, rowNumber, payload, reason, false, now, cancellationToken).ConfigureAwait(false);
                    results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                    skipped++;
                    continue;
                }

                if (!int.TryParse(opNumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var opNumber))
                {
                    const string reason = "Некорректный номер операции.";
                    await AddJobItemAsync(job.Id, rowNumber, payload, reason, false, now, cancellationToken).ConfigureAwait(false);
                    results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                    skipped++;
                    continue;
                }

                if (!TryParseDecimal(normText, out var normHours))
                {
                    const string reason = "Некорректный норматив.";
                    await AddJobItemAsync(job.Id, rowNumber, payload, reason, false, now, cancellationToken).ConfigureAwait(false);
                    results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                    skipped++;
                    continue;
                }

                normHours = Math.Round(normHours, 3, MidpointRounding.AwayFromZero);

                var part = await ResolvePartAsync(partName, partCode, partCache, cancellationToken).ConfigureAwait(false);
                var section = await ResolveSectionAsync(sectionName, sectionCache, cancellationToken).ConfigureAwait(false);
                var operation = await ResolveOperationAsync(operationName, operationCache, cancellationToken).ConfigureAwait(false);

                var routeKey = $"{part.Id}:{opNumber}";
                if (!routeCache.TryGetValue(routeKey, out var route))
                {
                    route = await _dbContext.PartRoutes
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

                    routeCache[routeKey] = route;
                }

                route.OperationId = operation.Id;
                route.SectionId = section.Id;
                route.NormHours = normHours;

                await AddJobItemAsync(job.Id, rowNumber, payload, null, true, now, cancellationToken).ConfigureAwait(false);
                results.Add(new ImportItemResultDto(rowNumber, "Saved", null));
                saved++;
            }

            job.Status = skipped > 0 ? "CompletedWithWarnings" : "Completed";
            job.CompletedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ImportSummaryDto(job.Id, rows.Count, saved, skipped, results);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task AddJobItemAsync(Guid jobId, int rowNumber, string payload, string? error, bool saved, DateTime now, CancellationToken cancellationToken)
    {
        var item = new ImportJobItem
        {
            Id = Guid.NewGuid(),
            ImportJobId = jobId,
            ExternalId = rowNumber.ToString(CultureInfo.InvariantCulture),
            Status = saved ? "Saved" : "Skipped",
            Payload = payload,
            ErrorMessage = error,
            CreatedAt = now,
            ProcessedAt = now,
        };

        await _dbContext.ImportJobItems.AddAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Part> ResolvePartAsync(string name, string code, Dictionary<string, Part> cache, CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(code) ? $"NAME:{name.ToUpperInvariant()}" : $"CODE:{code.ToUpperInvariant()}";
        if (cache.TryGetValue(key, out var cached))
        {
            cached.Name = name;
            cached.Code = string.IsNullOrWhiteSpace(code) ? null : code;
            return cached;
        }

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
                Name = name,
                Code = string.IsNullOrWhiteSpace(code) ? null : code,
            };

            await _dbContext.Parts.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity.Name = name;
            entity.Code = string.IsNullOrWhiteSpace(code) ? null : code;
        }

        cache[key] = entity;
        return entity;
    }

    private async Task<Section> ResolveSectionAsync(string name, Dictionary<string, Section> cache, CancellationToken cancellationToken)
    {
        var key = name.ToUpperInvariant();
        if (cache.TryGetValue(key, out var cached))
        {
            cached.Name = name;
            return cached;
        }

        var entity = await _dbContext.Sections.FirstOrDefaultAsync(x => x.Name == name, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new Section
            {
                Id = Guid.NewGuid(),
                Name = name,
            };

            await _dbContext.Sections.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity.Name = name;
        }

        cache[key] = entity;
        return entity;
    }

    private async Task<Operation> ResolveOperationAsync(string name, Dictionary<string, Operation> cache, CancellationToken cancellationToken)
    {
        var key = name.ToUpperInvariant();
        if (cache.TryGetValue(key, out var cached))
        {
            cached.Name = name;
            return cached;
        }

        var entity = await _dbContext.Operations.FirstOrDefaultAsync(x => x.Name == name, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new Operation
            {
                Id = Guid.NewGuid(),
                Name = name,
            };

            await _dbContext.Operations.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity.Name = name;
        }

        cache[key] = entity;
        return entity;
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        var normalized = value.Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
