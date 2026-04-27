using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        var warnings = new List<MetalDataImportWarningDto>();
        var materialsImported = 0;
        var materialsCreated = 0;
        var materialsUpdated = 0;
        var materialsSkipped = 0;
        var partsFound = 0;
        var partsCreated = 0;
        var normsCreated = 0;
        var normsUpdated = 0;
        var normsSkipped = 0;
        var normDuplicates = 0;
        var normConflicts = 0;
        var rowsSkipped = 0;
        var materialPreviewRows = new List<MetalMaterialImportPreviewRowDto>();
        var parsePreviewRows = new List<MetalDataParsePreviewRowDto>();
        var materialCache = new Dictionary<string, MetalMaterial>(StringComparer.OrdinalIgnoreCase);
        var partCache = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);

        if (mode is MetalImportMode.Materials or MetalImportMode.All)
        {
            var sheet = FindWorksheetOrThrow(
                workbook,
                ["Материалы и коэф. металлов", "Материалы и коэф металлов", "Материалы"]);
            var dbMaterials = await _dbContext.MetalMaterials.ToListAsync(cancellationToken).ConfigureAwait(false);
            var materialsByCode = dbMaterials
                .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                .GroupBy(x => NormalizeToken(x.Code!))
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            var materialsByName = dbMaterials
                .GroupBy(x => NormalizeToken(x.Name))
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            foreach (var row in (sheet.RangeUsed()?.RowsUsed().Skip(1) ?? []).Where(x => !x.IsEmpty()))
            {
                var rowNumber = row.RowNumber();
                var code = sheet.Cell(rowNumber, 2).GetString().Trim();
                var name = sheet.Cell(rowNumber, 3).GetString().Trim();
                var hasWeightPerUnit = TryParseDecimalCell(sheet.Cell(rowNumber, 4), out var weightPerUnitKg);
                var hasCoef = TryParseDecimalCell(sheet.Cell(rowNumber, 5), out var coefConsumption);
                var displayNameRaw = sheet.Cell(rowNumber, 6).GetString().Trim();
                var rowWarnings = new List<string>();

                if (string.IsNullOrWhiteSpace(name))
                {
                    materialsSkipped++;
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Пропущено обязательное поле Name."));
                    continue;
                }

                var normalizedCode = NormalizeToken(code);
                var normalizedName = NormalizeToken(name);
                var displayName = ResolveDisplayName(code, name, displayNameRaw);
                var unitInfo = ResolveUnitFromMaterialName(name);
                if (!unitInfo.Resolved)
                {
                    const string unresolvedMessage = "Не удалось автоматически определить тип единицы (m/m2) по названию материала.";
                    rowWarnings.Add(unresolvedMessage);
                    warnings.Add(new MetalDataImportWarningDto(rowNumber, sheet.Name, unresolvedMessage));
                }

                if ((!string.IsNullOrWhiteSpace(code) && code.Length > 64) || name.Length > 256 || displayName.Length > 256)
                {
                    materialsSkipped++;
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Превышена максимальная длина полей (Code/Name/DisplayName)."));
                    materialPreviewRows.Add(new MetalMaterialImportPreviewRowDto(
                        rowNumber,
                        string.IsNullOrWhiteSpace(code) ? null : code,
                        name,
                        displayName,
                        "skipped",
                        unitInfo.UnitKind,
                        unitInfo.StockUnit,
                        !unitInfo.Resolved,
                        rowWarnings));
                    continue;
                }

                if (!hasWeightPerUnit)
                {
                    materialsSkipped++;
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Пропущено обязательное поле WeightPerUnitKg."));
                    materialPreviewRows.Add(new MetalMaterialImportPreviewRowDto(
                        rowNumber,
                        string.IsNullOrWhiteSpace(code) ? null : code,
                        name,
                        displayName,
                        "skipped",
                        unitInfo.UnitKind,
                        unitInfo.StockUnit,
                        !unitInfo.Resolved,
                        rowWarnings));
                    continue;
                }

                var coefValue = hasCoef ? coefConsumption : 1m;
                if (!hasCoef)
                {
                    const string coefDefaultMessage = "Коэффициент не заполнен, применено значение 1.";
                    rowWarnings.Add(coefDefaultMessage);
                    warnings.Add(new MetalDataImportWarningDto(rowNumber, sheet.Name, coefDefaultMessage));
                }

                if (!unitInfo.Resolved)
                {
                    materialsSkipped++;
                    rowsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Строка пропущена: не удалось определить UnitKind/StockUnit по названию материала."));
                    materialPreviewRows.Add(new MetalMaterialImportPreviewRowDto(
                        rowNumber,
                        string.IsNullOrWhiteSpace(code) ? null : code,
                        name,
                        displayName,
                        "skipped",
                        unitInfo.UnitKind,
                        unitInfo.StockUnit,
                        true,
                        rowWarnings));
                    continue;
                }

                var materialKey = !string.IsNullOrWhiteSpace(normalizedCode)
                    ? $"CODE:{normalizedCode}"
                    : $"NAME:{normalizedName}";

                var isCreated = false;
                if (!materialCache.TryGetValue(materialKey, out var entity))
                {
                    entity = !string.IsNullOrWhiteSpace(normalizedCode)
                        ? materialsByCode.GetValueOrDefault(normalizedCode)
                        : materialsByName.GetValueOrDefault(normalizedName);

                    if (entity is null)
                    {
                        isCreated = true;
                        entity = new MetalMaterial
                        {
                            Id = Guid.NewGuid(),
                            Code = string.IsNullOrWhiteSpace(code) ? null : code,
                            UnitKind = unitInfo.UnitKind,
                        };

                        if (!dryRun)
                        {
                            await _dbContext.MetalMaterials.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                        }

                        if (!string.IsNullOrWhiteSpace(normalizedCode))
                        {
                            materialsByCode[normalizedCode] = entity;
                        }

                        materialsByName[normalizedName] = entity;
                    }

                    materialCache[materialKey] = entity;
                }

                entity.Name = name;
                entity.MassPerMeterKg = unitInfo.UnitKind == "Meter" ? weightPerUnitKg : 0m;
                entity.MassPerSquareMeterKg = unitInfo.UnitKind == "SquareMeter" ? weightPerUnitKg : 0m;
                entity.CoefConsumption = coefValue == 0m ? 1m : coefValue;
                entity.StockUnit = unitInfo.StockUnit;
                entity.WeightPerUnitKg = weightPerUnitKg;
                entity.Coefficient = entity.CoefConsumption;
                entity.UnitKind = unitInfo.UnitKind;
                entity.DisplayName = displayName;
                entity.IsActive = true;
                materialsImported++;
                if (isCreated)
                {
                    materialsCreated++;
                }
                else
                {
                    materialsUpdated++;
                }

                materialPreviewRows.Add(new MetalMaterialImportPreviewRowDto(
                    rowNumber,
                    string.IsNullOrWhiteSpace(code) ? null : code,
                    name,
                    displayName,
                    isCreated ? "created" : "updated",
                    unitInfo.UnitKind,
                    unitInfo.StockUnit,
                    false,
                    rowWarnings));
            }
        }

        if (mode is MetalImportMode.Norms or MetalImportMode.All)
        {
            var sheet = FindWorksheetOrThrow(
                workbook,
                ["Детали - Размеры - Нормы", "Детали-Размеры-Нормы", "Детали Размеры Нормы", "Детали"],
                ["Обозначение", "Наименование", "Размеры", "Ед"]);
            var fileNormKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in (sheet.RangeUsed()?.RowsUsed().Skip(1) ?? []).Where(x => !x.IsEmpty()))
            {
                var rowNumber = row.RowNumber();
                var code = row.Cell(1).GetString().Trim();
                var name = row.Cell(2).GetString().Trim();
                var sizeRaw = row.Cell(3).GetString().Trim();
                var consumptionTextRaw = row.Cell(4).GetString().Trim();
                var unit = row.Cell(6).GetString().Trim();
                var normalizedSizeRaw = NormalizeSizeRaw(sizeRaw);
                var normalizedUnit = NormalizeConsumptionUnit(unit);
                var parseResult = MetalSizeParser.Parse(sizeRaw, normalizedUnit, null);
                parsePreviewRows.Add(new MetalDataParsePreviewRowDto(
                    rowNumber,
                    code,
                    string.IsNullOrWhiteSpace(name) ? null : name,
                    string.IsNullOrWhiteSpace(sizeRaw) ? null : sizeRaw,
                    parseResult.ShapeType,
                    parseResult.DiameterMm,
                    parseResult.ThicknessMm,
                    parseResult.WidthMm,
                    parseResult.LengthMm,
                    parseResult.UnitNorm,
                    parseResult.ValueNorm,
                    parseResult.ParseStatus,
                    parseResult.ParseError));

                if (!TryParseDecimalCell(row.Cell(5), out var baseQty) || (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name)))
                {
                    rowsSkipped++;
                    normsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Пропущены обязательные поля Обозначение/Наименование/BaseConsumptionQty."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(normalizedUnit))
                {
                    rowsSkipped++;
                    normsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Пропущено обязательное поле Unit (единица измерения)."));
                    continue;
                }

                if ((!string.IsNullOrWhiteSpace(code) && code.Length > 256) || (!string.IsNullOrWhiteSpace(name) && name.Length > 256) || sizeRaw.Length > 128 || unit.Length > 16)
                {
                    rowsSkipped++;
                    normsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Превышена максимальная длина полей (Code/Name/Size/Unit)."));
                    continue;
                }

                var normalizedCodeRaw = NormalizePartCodeRaw(code);
                var canonicalCode = CanonicalizePartCode(code);
                var partKey = !string.IsNullOrWhiteSpace(code)
                    ? $"CODE:{normalizedCodeRaw}"
                    : $"NAME:{name.ToUpperInvariant()}";
                if (!partCache.TryGetValue(partKey, out var part))
                {
                    part = !string.IsNullOrWhiteSpace(normalizedCodeRaw)
                        ? await _dbContext.Parts
                            .FirstOrDefaultAsync(
                                x => (x.CodeRaw != null && x.CodeRaw == normalizedCodeRaw)
                                    || (x.Code == canonicalCode),
                                cancellationToken)
                            .ConfigureAwait(false)
                        : await _dbContext.Parts.FirstOrDefaultAsync(x => x.Code == null && x.Name == name, cancellationToken).ConfigureAwait(false);
                    if (part is null)
                    {
                        partsCreated++;
                        part = new Part
                        {
                            Id = Guid.NewGuid(),
                            Code = canonicalCode,
                            CodeRaw = normalizedCodeRaw,
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

                    partCache[partKey] = part;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    part.Name = name;
                }

                part.Code = canonicalCode;
                part.CodeRaw = normalizedCodeRaw;

                var normKeyHash = ComputeNormKeyHash(part.Id, normalizedSizeRaw, normalizedUnit, baseQty, null);
                if (!fileNormKeys.Add(normKeyHash))
                {
                    normsSkipped++;
                    normDuplicates++;
                    warnings.Add(new MetalDataImportWarningDto(rowNumber, sheet.Name, "Точная дублирующая строка в файле: запись не создана повторно."));
                    continue;
                }

                var existingNorms = await _dbContext.MetalConsumptionNorms
                    .Where(x => x.PartId == part.Id && x.IsActive)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                var norm = existingNorms.FirstOrDefault(x =>
                    x.NormKeyHash == normKeyHash
                    || IsNaturalNormMatch(x, normalizedSizeRaw, normalizedUnit, baseQty, null));
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
                    var hasConflictInText = !string.IsNullOrWhiteSpace(consumptionTextRaw)
                        && !string.Equals((norm.ConsumptionTextRaw ?? string.Empty).Trim(), consumptionTextRaw, StringComparison.OrdinalIgnoreCase);
                    if (hasConflictInText)
                    {
                        normConflicts++;
                        warnings.Add(new MetalDataImportWarningDto(rowNumber, sheet.Name, "Найдена строка с тем же natural key, но отличающимся текстом нормы: запись обновлена."));
                    }
                }

                if (existingNorms.Any(x =>
                        x.NormKeyHash != normKeyHash
                        && string.Equals(x.NormalizedSizeRaw, normalizedSizeRaw, StringComparison.Ordinal)
                        && string.Equals(x.NormalizedConsumptionUnit, normalizedUnit, StringComparison.Ordinal)
                        && x.MetalMaterialId == null))
                {
                    warnings.Add(new MetalDataImportWarningDto(rowNumber, sheet.Name, "Для детали уже есть другая активная норма с тем же размером/единицей и иным значением."));
                }

                var comment = consumptionTextRaw;
                if (comment.Length > 256)
                {
                    rowsSkipped++;
                    normsSkipped++;
                    errors.Add(new MetalDataImportErrorDto(rowNumber, sheet.Name, "Превышена максимальная длина поля Comment."));
                    continue;
                }

                norm.SizeRaw = sizeRaw;
                norm.NormalizedSizeRaw = normalizedSizeRaw;
                norm.NormKeyHash = normKeyHash;
                norm.ConsumptionTextRaw = consumptionTextRaw.Length == 0 ? null : consumptionTextRaw;
                norm.ShapeType = parseResult.ShapeType;
                norm.DiameterMm = parseResult.DiameterMm;
                norm.ThicknessMm = parseResult.ThicknessMm;
                norm.WidthMm = parseResult.WidthMm;
                norm.LengthMm = parseResult.LengthMm;
                norm.UnitNorm = parseResult.UnitNorm;
                norm.ValueNorm = parseResult.ValueNorm ?? baseQty;
                norm.ParseStatus = parseResult.ParseStatus;
                norm.ParseError = parseResult.ParseError;
                norm.BaseConsumptionQty = baseQty;
                norm.ConsumptionUnit = normalizedUnit;
                norm.NormalizedConsumptionUnit = normalizedUnit;
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
            materialsCreated,
            materialsUpdated,
            materialsSkipped,
            partsFound,
            partsCreated,
            normsCreated,
            normsUpdated,
            normsSkipped,
            normDuplicates,
            normConflicts,
            rowsSkipped,
            materialPreviewRows.Count,
            materialPreviewRows,
            parsePreviewRows.Count,
            parsePreviewRows,
            warnings,
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

    private static (bool Resolved, string UnitKind, string StockUnit) ResolveUnitFromMaterialName(string name)
    {
        var haystack = (name ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return (false, "Unknown", "unknown");
        }

        if (haystack.Contains("лист", StringComparison.Ordinal)
            || haystack.Contains("лента", StringComparison.Ordinal)
            || haystack.Contains("пластин", StringComparison.Ordinal))
        {
            return (true, "SquareMeter", "m2");
        }

        if (haystack.Contains("круг", StringComparison.Ordinal)
            || haystack.Contains("прут", StringComparison.Ordinal)
            || haystack.Contains("труб", StringComparison.Ordinal)
            || haystack.Contains("профил", StringComparison.Ordinal)
            || haystack.Contains("полос", StringComparison.Ordinal)
            || haystack.Contains("шин", StringComparison.Ordinal))
        {
            return (true, "Meter", "m");
        }

        return (false, "Unknown", "unknown");
    }

    private static string ResolveDisplayName(string code, string name, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return string.IsNullOrWhiteSpace(code)
            ? name
            : $"{code.Trim()} | {name}";
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

    private static string NormalizeSizeRaw(string value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return raw
            .Replace('Х', 'x')
            .Replace('х', 'x')
            .Replace('*', 'x')
            .Replace('×', 'x')
            .Replace("  ", " ", StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private static string NormalizeConsumptionUnit(string value)
    {
        var raw = (value ?? string.Empty).Trim().ToLowerInvariant();
        return raw switch
        {
            "м" or "m" => "m",
            "м2" or "м²" or "m2" => "m2",
            "кг" or "kg" => "kg",
            "г" or "гр" or "g" => "g",
            _ => string.Empty
        };
    }

    private static string? NormalizePartCodeRaw(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.Trim().ToUpperInvariant();
    }

    private static string? CanonicalizePartCode(string code)
    {
        var raw = NormalizePartCodeRaw(code);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.Length <= 64)
        {
            return raw;
        }

        var prefix = raw[..52];
        var suffix = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(raw)))[..11];
        return $"{prefix}_{suffix}";
    }

    private static string ComputeNormKeyHash(Guid partId, string normalizedSizeRaw, string normalizedUnit, decimal baseQty, Guid? metalMaterialId)
    {
        var payload = string.Join("|",
            partId.ToString("N"),
            normalizedSizeRaw,
            normalizedUnit,
            baseQty.ToString("0.######", CultureInfo.InvariantCulture),
            metalMaterialId?.ToString("N") ?? "NULL");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool IsNaturalNormMatch(MetalConsumptionNorm norm, string normalizedSizeRaw, string normalizedUnit, decimal baseQty, Guid? metalMaterialId)
    {
        if (!string.IsNullOrWhiteSpace(norm.NormKeyHash))
        {
            return false;
        }

        var normSize = NormalizeSizeRaw(norm.NormalizedSizeRaw.Length > 0 ? norm.NormalizedSizeRaw : norm.SizeRaw ?? string.Empty);
        var normUnit = NormalizeConsumptionUnit(norm.NormalizedConsumptionUnit.Length > 0 ? norm.NormalizedConsumptionUnit : norm.ConsumptionUnit);
        return string.Equals(normSize, normalizedSizeRaw, StringComparison.Ordinal)
            && string.Equals(normUnit, normalizedUnit, StringComparison.Ordinal)
            && norm.BaseConsumptionQty == baseQty
            && norm.MetalMaterialId == metalMaterialId;
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
