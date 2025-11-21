using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Imports;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;

namespace UchetNZP.Application.Services;

public class ImportService : IImportService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ImportService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<ImportSummaryDto> ImportRoutesExcelAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Имя файла не заполнено.", nameof(fileName));
        }

        var resolvedFileName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrEmpty(resolvedFileName))
        {
            throw new ArgumentException("Имя файла не заполнено.", nameof(fileName));
        }

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var headerRow = worksheet.FirstRowUsed() ?? throw new InvalidOperationException("Пустой файл Excel.");
        var headerCells = headerRow.CellsUsed().OrderBy(cell => cell.Address.ColumnNumber).ToList();

        using var errorWorkbook = new XLWorkbook();
        var errorWorksheet = errorWorkbook.Worksheets.Add("Ошибки");
        var errorRowIndex = 2;
        var hasErrors = false;

        void RegisterErrorRow(IXLRangeRow row, string reason, int rowNumber)
        {
            if (!hasErrors)
            {
                var header = errorWorksheet.Row(1);
                header.Cell(1).Value = "Номер строки";
                var headerColumn = 2;
                foreach (var cell in headerCells)
                {
                    header.Cell(headerColumn++).Value = cell.GetString();
                }

                header.Cell(headerColumn).Value = "Ошибка";
                hasErrors = true;
            }

            var targetRow = errorWorksheet.Row(errorRowIndex++);
            targetRow.Cell(1).Value = rowNumber;

            var columnIndex = 2;
            foreach (var cell in headerCells)
            {
                targetRow.Cell(columnIndex++).Value = row.Cell(cell.Address.ColumnNumber).Value;
            }

            targetRow.Cell(columnIndex).Value = reason;
        }

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var headerText = cell.GetString().Trim();

            if (string.IsNullOrEmpty(headerText))
            {
                continue;
            }

            if (!headers.ContainsKey(headerText))
            {
                headers[headerText] = cell.Address.ColumnNumber;
            }
        }

        int Require(params string[] names)
        {
            foreach (var name in names)
            {
                if (headers.TryGetValue(name, out var column))
                {
                    return column;
                }
            }

            if (names.Length == 0)
            {
                throw new InvalidOperationException("Не указаны имена столбцов.");
            }

            var display = string.Join("' или '", names);
            throw new InvalidOperationException($"Не найден столбец '{display}'.");
        }

        int Optional(params string[] names)
        {
            foreach (var name in names)
            {
                if (headers.TryGetValue(name, out var column))
                {
                    return column;
                }
            }

            return -1;
        }

        var colPartName = Require("Наименование детали");
        var colPartCode = Optional("Обозначение", "№ чертежа");
        var colOperationName = Require("Наименование операции");
        var colOpNumber = Require("№ операции");
        var colNorm = Require("Утвержденный норматив (н/ч)", "Технологический процесс");
        var colSection = Optional("Вид работ", "Участок");
        var colRemaining = Optional("Количество остатка");

        var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1).ToList() ?? new List<IXLRangeRow>();

        var now = DateTime.UtcNow;
        var job = new ImportJob
        {
            Id = Guid.NewGuid(),
            Ts = now,
            UserId = _currentUserService.UserId,
            FileName = resolvedFileName,
            TotalRows = rows.Count,
            Succeeded = 0,
            Skipped = 0,
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _dbContext.ImportJobs.AddAsync(job, cancellationToken).ConfigureAwait(false);

            var partCache = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);
            var sectionCache = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
            var operationCache = new Dictionary<string, Operation>(StringComparer.OrdinalIgnoreCase);
            var routeCache = new Dictionary<string, PartRoute>(StringComparer.OrdinalIgnoreCase);
            var wipBalanceCache = new Dictionary<string, WipBalance>(StringComparer.OrdinalIgnoreCase);

            var results = new List<ImportItemResultDto>();
            var items = new List<ImportJobItem>();
            var succeeded = 0;
            var skipped = 0;

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rowNumber = row.RowNumber();

                string GetString(int column)
                {
                    return column > 0 ? row.Cell(column).GetString().Trim() : string.Empty;
                }

                var rawPartName = GetString(colPartName);
                var partCode = GetString(colPartCode);
                var partName = CombinePartName(rawPartName, partCode);
                var operationName = GetString(colOperationName);
                var opNumberText = GetString(colOpNumber);
                var normText = GetString(colNorm);
                var sectionName = GetSectionName(row, colSection, operationName);
                var remainingText = GetString(colRemaining);
                decimal? remainingQuantity = null;

                if (!string.IsNullOrWhiteSpace(remainingText))
                {
                    if (!TryParseDecimal(remainingText, out var parsedRemaining))
                    {
                        const string reason = "Некорректное количество остатка.";
                        items.Add(CreateJobItem(job.Id, rowNumber, "Skipped", reason));
                        results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                        RegisterErrorRow(row, reason, rowNumber);
                        skipped++;
                        continue;
                    }

                    parsedRemaining = Math.Round(parsedRemaining, 3, MidpointRounding.AwayFromZero);

                    if (parsedRemaining < 0)
                    {
                        const string reason = "Количество остатка не может быть отрицательным.";
                        items.Add(CreateJobItem(job.Id, rowNumber, "Skipped", reason));
                        results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                        RegisterErrorRow(row, reason, rowNumber);
                        skipped++;
                        continue;
                    }

                    remainingQuantity = parsedRemaining;
                }

                if (string.IsNullOrWhiteSpace(partName) || string.IsNullOrWhiteSpace(operationName) || string.IsNullOrWhiteSpace(opNumberText) || string.IsNullOrWhiteSpace(normText) || string.IsNullOrWhiteSpace(sectionName))
                {
                    const string reason = "Пропущены обязательные поля.";
                    items.Add(CreateJobItem(job.Id, rowNumber, "Skipped", reason));
                    results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                    RegisterErrorRow(row, reason, rowNumber);
                    skipped++;
                    continue;
                }

                if (!OperationNumber.TryParse(opNumberText, out var opNumber))
                {
                    const string reason = "Некорректный номер операции.";
                    items.Add(CreateJobItem(job.Id, rowNumber, "Skipped", reason));
                    results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                    RegisterErrorRow(row, reason, rowNumber);
                    skipped++;
                    continue;
                }

                if (!TryParseDecimal(normText, out var normHours))
                {
                    const string reason = "Некорректный норматив.";
                    items.Add(CreateJobItem(job.Id, rowNumber, "Skipped", reason));
                    results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                    RegisterErrorRow(row, reason, rowNumber);
                    skipped++;
                    continue;
                }

                normHours = Math.Round(normHours, 3, MidpointRounding.AwayFromZero);

                var part = await ResolvePartAsync(partName, partCode, partCache, cancellationToken).ConfigureAwait(false);
                var section = await ResolveSectionAsync(sectionName, sectionCache, cancellationToken).ConfigureAwait(false);
                var operation = await ResolveOperationAsync(operationName, operationCache, cancellationToken).ConfigureAwait(false);

                var routeKey = $"{part.Id}:{opNumber}";
                var isNewRoute = false;
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
                        isNewRoute = true;
                    }

                    routeCache[routeKey] = route;
                }

                if (!isNewRoute)
                {
                    var existingNames = await GetRouteNamesAsync(route, cancellationToken).ConfigureAwait(false);
                    if (!NamesEqual(existingNames.OperationName, operation.Name) || !NamesEqual(existingNames.SectionName, section.Name))
                    {
                        var reason = "В файле указан другой вид работ или наименование операции для уже существующей строки.";
                        items.Add(CreateJobItem(job.Id, rowNumber, "Skipped", reason));
                        results.Add(new ImportItemResultDto(rowNumber, "Skipped", reason));
                        RegisterErrorRow(row, reason, rowNumber);
                        skipped++;
                        continue;
                    }
                }

                route.OperationId = operation.Id;
                route.SectionId = section.Id;
                route.NormHours = normHours;

                if (remainingQuantity.HasValue)
                {
                    await UpsertWipBalanceAsync(part.Id, section.Id, opNumber, remainingQuantity.Value, wipBalanceCache, cancellationToken)
                        .ConfigureAwait(false);
                }

                items.Add(CreateJobItem(job.Id, rowNumber, "Succeeded", null));
                results.Add(new ImportItemResultDto(rowNumber, "Succeeded", null));
                succeeded++;
            }

            job.TotalRows = rows.Count;
            job.Succeeded = succeeded;
            job.Skipped = skipped;

            if (items.Count > 0)
            {
                await _dbContext.ImportJobItems.AddRangeAsync(items, cancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            byte[]? errorFileContent = null;
            string? errorFileName = null;

            if (hasErrors)
            {
                errorWorksheet.Columns().AdjustToContents();
                using var errorStream = new MemoryStream();
                errorWorkbook.SaveAs(errorStream);
                errorFileContent = errorStream.ToArray();
                errorFileName = $"{Path.GetFileNameWithoutExtension(job.FileName)}_Ошибки.xlsx";
            }

            return new ImportSummaryDto(job.Id, job.FileName, job.TotalRows, job.Succeeded, job.Skipped, results, errorFileName, errorFileContent);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static ImportJobItem CreateJobItem(Guid jobId, int rowIndex, string status, string? message)
    {
        return new ImportJobItem
        {
            Id = Guid.NewGuid(),
            ImportJobId = jobId,
            RowIndex = rowIndex,
            Status = status,
            Message = message,
        };
    }

    private static string CombinePartName(string name, string code)
    {
        name = name?.Trim() ?? string.Empty;
        code = code?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            return code;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return name;
        }

        return $"{name} {code}";
    }

    private static string GetSectionName(IXLRangeRow row, int column, string operationName)
    {
        if (column <= 0)
        {
            return operationName;
        }

        var value = row.Cell(column).GetString().Trim();
        return string.IsNullOrWhiteSpace(value) ? operationName : value;
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

    private async Task<WipBalance> UpsertWipBalanceAsync(
        Guid partId,
        Guid sectionId,
        int opNumber,
        decimal quantity,
        Dictionary<string, WipBalance> cache,
        CancellationToken cancellationToken)
    {
        var key = $"{partId:N}:{sectionId:N}:{opNumber}";

        if (!cache.TryGetValue(key, out var balance))
        {
            balance = await _dbContext.WipBalances
                .FirstOrDefaultAsync(x => x.PartId == partId && x.SectionId == sectionId && x.OpNumber == opNumber, cancellationToken)
                .ConfigureAwait(false);

            if (balance is not null)
            {
                cache[key] = balance;
            }
        }

        if (balance is null)
        {
            balance = new WipBalance
            {
                Id = Guid.NewGuid(),
                PartId = partId,
                SectionId = sectionId,
                OpNumber = opNumber,
            };

            await _dbContext.WipBalances.AddAsync(balance, cancellationToken).ConfigureAwait(false);
        }

        balance.Quantity = quantity;
        cache[key] = balance;

        return balance;
    }

    private async Task<(string OperationName, string SectionName)> GetRouteNamesAsync(PartRoute route, CancellationToken cancellationToken)
    {
        var operationName = route.Operation?.Name;
        if (string.IsNullOrWhiteSpace(operationName))
        {
            operationName = await _dbContext.Operations
                .Where(x => x.Id == route.OperationId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var sectionName = route.Section?.Name;
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            sectionName = await _dbContext.Sections
                .Where(x => x.Id == route.SectionId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return (operationName ?? string.Empty, sectionName ?? string.Empty);
    }

    private static bool NamesEqual(string left, string right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        var normalized = value.Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
