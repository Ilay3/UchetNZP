using System.Globalization;
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
        var colPartCode = Optional("Код детали", "Обозначение", "№ чертежа");
        var colOpNumber = Require("№ операции");
        var colOperationName = Require("Наименование операции");
        var colSection = Require("Вид работ", "Участок");
        var colNorm = Require("Норматив, н/ч", "Утвержденный норматив (н/ч)", "Технологический процесс");
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

                var partName = GetString(colPartName);
                var partCode = GetString(colPartCode);
                var operationName = GetString(colOperationName);
                var opNumberText = GetString(colOpNumber);
                var normText = GetString(colNorm);
                var sectionName = GetSectionName(row, colSection);
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

    public async Task<MetalDataImportSummaryDto> ImportMetalDataExcelAsync(
        Stream stream,
        string fileName,
        MetalImportMode mode,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(stream);
        var sourceFileName = Path.GetFileName(fileName.Trim());
        var errors = new List<MetalDataImportErrorDto>();
        var materialsImported = 0;
        var partsFound = 0;
        var partsCreated = 0;
        var normsCreated = 0;
        var normsUpdated = 0;
        var rowsSkipped = 0;
        var materialCache = new Dictionary<string, MetalMaterial>(StringComparer.OrdinalIgnoreCase);
        var partCache = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);

        if (mode is MetalImportMode.Materials or MetalImportMode.All)
        {
            var sheet = FindWorksheetOrThrow(
                workbook,
                ["Материалы и коэф. металлов", "Материалы и коэф металлов", "Материалы"]);
            foreach (var row in (sheet.RangeUsed()?.RowsUsed().Skip(1) ?? []).Where(x => !x.IsEmpty()))
            {
                var rowNumber = row.RowNumber();
                var code = row.Cell(2).GetString().Trim();
                var name = row.Cell(3).GetString().Trim();
                var weight = TryParseDecimalCell(row.Cell(4), out var weightValue) ? weightValue : (decimal?)null;
                var coefficient = TryParseDecimalCell(row.Cell(5), out var coeffValue) ? coeffValue : 1m;
                var displayName = row.Cell(6).GetString().Trim();

                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                {
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Пропущены обязательные поля Code/Name."));
                    continue;
                }

                if (code.Length > 64 || name.Length > 256 || (!string.IsNullOrWhiteSpace(displayName) && displayName.Length > 256))
                {
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Превышена максимальная длина полей (Code/Name/DisplayName)."));
                    continue;
                }

                if (!materialCache.TryGetValue(code, out var entity))
                {
                    entity = await _dbContext.MetalMaterials.FirstOrDefaultAsync(x => x.Code == code, cancellationToken).ConfigureAwait(false);
                    if (entity is null)
                    {
                        entity = new MetalMaterial
                        {
                            Id = Guid.NewGuid(),
                            Code = code,
                            UnitKind = "Unknown",
                        };

                        if (!dryRun)
                        {
                            await _dbContext.MetalMaterials.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    materialCache[code] = entity;
                }

                entity.Name = name;
                entity.WeightPerUnitKg = weight;
                entity.Coefficient = coefficient == 0m ? 1m : coefficient;
                entity.DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
                entity.IsActive = true;
                materialsImported++;
            }
        }

        if (mode is MetalImportMode.Norms or MetalImportMode.All)
        {
            var sheet = FindWorksheetOrThrow(
                workbook,
                ["Детали - Размеры - Нормы", "Детали-Размеры-Нормы", "Детали Размеры Нормы", "Детали"],
                ["Обозначение", "На деталь", "Материал"]);
            foreach (var row in (sheet.RangeUsed()?.RowsUsed().Skip(1) ?? []).Where(x => !x.IsEmpty()))
            {
                var rowNumber = row.RowNumber();
                var code = row.Cell(1).GetString().Trim();
                var name = row.Cell(2).GetString().Trim();
                var sizeRaw = row.Cell(3).GetString().Trim();
                var unit = row.Cell(6).GetString().Trim();

                if (string.IsNullOrWhiteSpace(code) || !TryParseDecimalCell(row.Cell(5), out var baseQty))
                {
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Пропущены обязательные поля Обозначение/BaseConsumptionQty."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(unit))
                {
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Пропущено обязательное поле Unit (единица измерения)."));
                    continue;
                }

                if (code.Length > 64 || name.Length > 256 || sizeRaw.Length > 128 || unit.Length > 16)
                {
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Превышена максимальная длина полей (Code/Name/Size/Unit)."));
                    continue;
                }

                if (!partCache.TryGetValue(code, out var part))
                {
                    part = await _dbContext.Parts.FirstOrDefaultAsync(x => x.Code == code, cancellationToken).ConfigureAwait(false);
                    if (part is null)
                    {
                        partsCreated++;
                        part = new Part
                        {
                            Id = Guid.NewGuid(),
                            Code = code,
                            Name = string.IsNullOrWhiteSpace(name) ? code : name,
                        };

                        if (!dryRun)
                        {
                            await _dbContext.Parts.AddAsync(part, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        partsFound++;
                    }

                    partCache[code] = part;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    part.Name = name;
                }

                var norm = await _dbContext.MetalConsumptionNorms
                    .FirstOrDefaultAsync(x => x.PartId == part.Id && x.IsActive, cancellationToken)
                    .ConfigureAwait(false);

                if (norm is null)
                {
                    normsCreated++;
                    norm = new MetalConsumptionNorm
                    {
                        Id = Guid.NewGuid(),
                        PartId = part.Id,
                        IsActive = true,
                    };

                    if (!dryRun)
                    {
                        await _dbContext.MetalConsumptionNorms.AddAsync(norm, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    normsUpdated++;
                }

                var comment = row.Cell(4).GetString().Trim();
                if (comment.Length > 256)
                {
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Превышена максимальная длина поля Comment."));
                    continue;
                }

                norm.SizeRaw = sizeRaw;
                norm.BaseConsumptionQty = baseQty;
                norm.ConsumptionUnit = unit;
                norm.SourceFile = sourceFileName.Length > 256 ? sourceFileName[..256] : sourceFileName;
                norm.MetalMaterialId = null;
                norm.Comment = comment;
                norm.IsActive = true;
            }
        }

        if (!dryRun)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                var baseMessage = ex.InnerException?.Message ?? ex.Message;
                var friendlyReason = baseMessage.Contains("IX_Parts_Code", StringComparison.OrdinalIgnoreCase)
                    ? "Найдены повторяющиеся значения поля 'Обозначение' (Code) для деталей."
                    : baseMessage.Contains("IX_MetalMaterials_Code", StringComparison.OrdinalIgnoreCase)
                        ? "Найдены повторяющиеся значения поля 'артикул' (Code) для материалов."
                        : "Ошибка сохранения данных в БД. Проверьте файл на дубли и обязательные поля.";

                rowsSkipped++;
                errors.Add(new MetalDataImportErrorDto(0, "Database", friendlyReason));
            }
        }

        byte[]? errorFileContent = null;
        string? errorFileName = null;
        if (errors.Count > 0)
        {
            using var errorWorkbook = BuildMetalImportErrorWorkbook(errors);
            using var errorStream = new MemoryStream();
            errorWorkbook.SaveAs(errorStream);
            errorFileContent = errorStream.ToArray();
            errorFileName = $"{Path.GetFileNameWithoutExtension(sourceFileName)}_Ошибки.xlsx";
        }

        return new MetalDataImportSummaryDto(
            sourceFileName,
            dryRun,
            materialsImported,
            partsFound,
            partsCreated,
            normsCreated,
            normsUpdated,
            rowsSkipped,
            errors,
            errorFileName,
            errorFileContent);
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

    private static string GetSectionName(IXLRangeRow row, int column)
    {
        if (column <= 0)
        {
            return string.Empty;
        }

        return row.Cell(column).GetString().Trim();
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

    private static bool TryParseDecimal(string value, out decimal result)
    {
        var normalized = value.Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDecimalCell(IXLCell cell, out decimal result)
    {
        if (cell.DataType == XLDataType.Number)
        {
            result = Convert.ToDecimal(cell.GetDouble(), CultureInfo.InvariantCulture);
            return true;
        }

        return TryParseDecimal(cell.GetString().Trim(), out result);
    }

    private static IXLWorksheet FindWorksheetOrThrow(
        XLWorkbook workbook,
        IReadOnlyCollection<string> expectedNames,
        IReadOnlyCollection<string>? requiredHeaderParts = null)
    {
        foreach (var worksheet in workbook.Worksheets)
        {
            if (expectedNames.Any(name => SheetNameEquals(worksheet.Name, name)))
            {
                return worksheet;
            }
        }

        if (requiredHeaderParts is { Count: > 0 })
        {
            var normalizedHeaderParts = requiredHeaderParts.Select(NormalizeToken).Where(x => x.Length > 0).ToArray();
            foreach (var worksheet in workbook.Worksheets)
            {
                var headerRowText = GetHeaderProbeText(worksheet);
                if (normalizedHeaderParts.All(headerRowText.Contains))
                {
                    return worksheet;
                }
            }
        }

        var availableSheets = string.Join(", ", workbook.Worksheets.Select(x => $"'{x.Name}'"));
        var expected = string.Join(", ", expectedNames.Select(x => $"'{x}'"));
        throw new InvalidOperationException(
            $"Не найден лист Excel. Ожидался один из: {expected}. Доступные листы: {availableSheets}.");
    }

    private static string GetHeaderProbeText(IXLWorksheet worksheet)
    {
        var firstUsedRow = worksheet.FirstRowUsed();
        if (firstUsedRow is null)
        {
            return string.Empty;
        }

        return NormalizeToken(firstUsedRow.CellsUsed().Select(c => c.GetString()).Aggregate(string.Empty, (a, b) => $"{a} {b}"));
    }

    private static bool SheetNameEquals(string actualName, string expectedName)
    {
        if (string.Equals(actualName.Trim(), expectedName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(NormalizeToken(actualName), NormalizeToken(expectedName), StringComparison.Ordinal);
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToUpperInvariant();
        normalized = normalized
            .Replace("Ё", "Е", StringComparison.Ordinal)
            .Replace('–', '-')
            .Replace('—', '-');

        var buffer = new char[normalized.Length];
        var index = 0;
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[index++] = ch;
            }
        }

        return new string(buffer, 0, index);
    }

    private static XLWorkbook BuildMetalImportErrorWorkbook(IReadOnlyCollection<MetalDataImportErrorDto> errors)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Ошибки");

        worksheet.Cell(1, 1).Value = "Лист";
        worksheet.Cell(1, 2).Value = "Номер строки";
        worksheet.Cell(1, 3).Value = "Описание ошибки";

        var rowIndex = 2;
        foreach (var error in errors)
        {
            worksheet.Cell(rowIndex, 1).Value = error.Sheet;
            worksheet.Cell(rowIndex, 2).Value = error.RowIndex;
            worksheet.Cell(rowIndex, 3).Value = error.Message;
            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();
        return workbook;
    }
}
