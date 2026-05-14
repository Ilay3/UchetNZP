using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Services;

public interface IMetalRequirementWarehousePrintDocumentService
{
    Task<MetalRequirementWarehousePrintDocumentResult> BuildAsync(Guid requirementId, CancellationToken cancellationToken = default);
    Task<MetalRequirementWarehousePrintDocumentResult> BuildPdfAsync(Guid requirementId, CancellationToken cancellationToken = default);
}

public sealed record MetalRequirementWarehousePrintDocumentResult(
    string FileName,
    byte[] Content,
    string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

public class MetalRequirementWarehousePrintDocumentService : IMetalRequirementWarehousePrintDocumentService
{
    private const string TemplateRelativePath = "Templates/Documents/Требование на склад.docx";

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IWordToPdfConverter? _wordToPdfConverter;

    public MetalRequirementWarehousePrintDocumentService(
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        IWordToPdfConverter? wordToPdfConverter = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _wordToPdfConverter = wordToPdfConverter;
    }

    public async Task<MetalRequirementWarehousePrintDocumentResult> BuildAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var requirement = await _dbContext.MetalRequirements
            .AsNoTracking()
            .Where(x => x.Id == requirementId)
            .Select(x => new RequirementProjection
            {
                Id = x.Id,
                RequirementNumber = x.RequirementNumber,
                RequirementDate = x.RequirementDate,
                Status = x.Status,
                CreatedBy = x.CreatedBy,
                UpdatedBy = x.UpdatedBy,
                WipLaunchId = x.WipLaunchId,
                LaunchDate = x.WipLaunch != null ? x.WipLaunch.LaunchDate : (DateTime?)null,
                PartCode = x.PartCode,
                PartName = x.PartName,
                Quantity = x.Quantity,
                Items = x.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new RequirementItemProjection
                    {
                        MaterialName = i.MetalMaterial != null ? i.MetalMaterial.Name : string.Empty,
                        MaterialCode = i.MetalMaterial != null ? i.MetalMaterial.Code : string.Empty,
                        NormPerUnit = i.NormPerUnit,
                        ConsumptionPerUnit = i.ConsumptionPerUnit,
                        Unit = i.Unit,
                        ConsumptionUnit = i.ConsumptionUnit,
                        TotalRequiredQty = i.TotalRequiredQty,
                        RequiredQty = i.RequiredQty,
                        TotalRequiredWeightKg = i.TotalRequiredWeightKg,
                        RequiredWeightKg = i.RequiredWeightKg,
                        SizeRaw = i.SizeRaw,
                        Comment = i.Comment,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (requirement is null)
        {
            throw new KeyNotFoundException($"Требование {requirementId} не найдено.");
        }

        var templatePath = Path.Combine(_environment.ContentRootPath, TemplateRelativePath);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Шаблон печатной формы требования на склад не найден в проекте.", templatePath);
        }

        await using var templateStream = File.OpenRead(templatePath);
        using var memoryStream = new MemoryStream();
        await templateStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        var items = requirement.Items.Select((item, index) => new RequirementPrintItem
        {
            RowNumber = (index + 1).ToString(CultureInfo.InvariantCulture),
            MaterialName = item.MaterialName ?? string.Empty,
            Material = BuildMaterial(item.MaterialName, item.MaterialCode),
            MaterialCode = item.MaterialCode ?? string.Empty,
            NormPerUnit = FormatDecimal(item.NormPerUnit > 0m ? item.NormPerUnit : item.ConsumptionPerUnit),
            Unit = Safe(item.Unit, item.ConsumptionUnit),
            TotalRequiredQty = FormatDecimal(item.TotalRequiredQty > 0m ? item.TotalRequiredQty : item.RequiredQty),
            TotalWeightKg = FormatDecimal(item.TotalRequiredWeightKg ?? item.RequiredWeightKg),
            SizeOrNote = Safe(item.SizeRaw, item.Comment),
        }).ToList();

        using (var document = WordprocessingDocument.Open(memoryStream, true))
        {
            var mainPart = document.MainDocumentPart;
            var mainDocument = mainPart?.Document;
            var body = mainDocument?.Body;
            if (body is not null && mainDocument is not null)
            {
                FillHeaderPlaceholders(body, requirement, items);
                FillItemsTable(body, items);
                FillM11Template(body, requirement, items);
                mainDocument.Save();
            }
        }

        var date = requirement.RequirementDate.ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var number = SanitizeFilePart(requirement.RequirementNumber);
        var fileName = $"Требование_на_склад_{number}_{date}.docx";
        return new MetalRequirementWarehousePrintDocumentResult(fileName, memoryStream.ToArray());
    }

    public async Task<MetalRequirementWarehousePrintDocumentResult> BuildPdfAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        if (_wordToPdfConverter is null)
        {
            throw new InvalidOperationException("Word to PDF converter is not configured.");
        }

        var docx = await BuildAsync(requirementId, cancellationToken).ConfigureAwait(false);
        var pdf = await _wordToPdfConverter
            .ConvertAsync(docx.FileName, docx.Content, "requirements", cancellationToken)
            .ConfigureAwait(false);

        return new MetalRequirementWarehousePrintDocumentResult(pdf.FileName, pdf.Content, pdf.ContentType);
    }

    private static void FillHeaderPlaceholders(Body body, RequirementProjection requirement, IReadOnlyCollection<RequirementPrintItem> items)
    {
        var firstItem = items.FirstOrDefault();
        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{REQ_NUMBER}}"] = requirement.RequirementNumber ?? string.Empty,
            ["{{REQ_DATE}}"] = requirement.RequirementDate.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ["{{WIP_LAUNCH}}"] = requirement.WipLaunchId.ToString(),
            ["{{WIP_LAUNCH_INFO}}"] = requirement.LaunchDate.HasValue
                ? $"{requirement.WipLaunchId} от {requirement.LaunchDate.Value.ToLocalTime():dd.MM.yyyy}"
                : requirement.WipLaunchId.ToString(),
            ["{{PART_CODE}}"] = requirement.PartCode ?? string.Empty,
            ["{{DRAWING_NUMBER}}"] = requirement.PartCode ?? string.Empty,
            ["{{PART_NAME}}"] = requirement.PartName ?? string.Empty,
            ["{{PART_QTY}}"] = FormatDecimal(requirement.Quantity),
            ["{{MATERIAL}}"] = firstItem?.Material ?? string.Empty,
            ["{{STATUS}}"] = requirement.Status ?? string.Empty,
            ["{{REQUIRED_TOTAL}}"] = firstItem?.TotalRequiredQty ?? string.Empty,
            ["{{TOTAL_KG}}"] = firstItem?.TotalWeightKg ?? string.Empty,
            ["{{SIZE_NOTE}}"] = firstItem?.SizeOrNote ?? string.Empty,
        };

        foreach (var placeholder in placeholders)
        {
            ReplaceToken(body, placeholder.Key, placeholder.Value);
        }
    }

    private static void FillItemsTable(Body body, IReadOnlyCollection<RequirementPrintItem> items)
    {
        var templateRow = body
            .Descendants<TableRow>()
            .FirstOrDefault(row => row.Descendants<Text>().Any(text =>
                text.Text.Contains("{{ROW_NO}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{ITEM_MATERIAL}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{ITEM_NORM_PER_UNIT}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{ITEM_UNIT}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{ITEM_REQUIRED_TOTAL}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{ITEM_TOTAL_KG}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{ITEM_SIZE_NOTE}}", StringComparison.Ordinal)));

        if (templateRow is null)
        {
            return;
        }

        if (items.Count == 0)
        {
            templateRow.Remove();
            return;
        }

        foreach (var item in items)
        {
            var row = (TableRow)templateRow.CloneNode(true);
            ReplaceToken(row, "{{ROW_NO}}", item.RowNumber);
            ReplaceToken(row, "{{ITEM_MATERIAL}}", item.Material);
            ReplaceToken(row, "{{ITEM_NORM_PER_UNIT}}", item.NormPerUnit);
            ReplaceToken(row, "{{ITEM_UNIT}}", item.Unit);
            ReplaceToken(row, "{{ITEM_REQUIRED_TOTAL}}", item.TotalRequiredQty);
            ReplaceToken(row, "{{ITEM_TOTAL_KG}}", item.TotalWeightKg);
            ReplaceToken(row, "{{ITEM_SIZE_NOTE}}", item.SizeOrNote);
            templateRow.Parent!.InsertBefore(row, templateRow);
        }

        templateRow.Remove();
    }

    private static void FillM11Template(Body body, RequirementProjection requirement, IReadOnlyList<RequirementPrintItem> items)
    {
        var tables = body.Elements<Table>().ToList();
        if (tables.Count < 4)
        {
            return;
        }

        var titleRows = tables[0].Elements<TableRow>().ToList();
        SetCellText(GetCell(titleRows, 0, 1), FormatRequirementNumberForPrint(requirement.RequirementNumber));
        SetCellText(GetCell(titleRows, 2, 1), "ООО Промавтоматика");

        var metaRows = tables[1].Elements<TableRow>().ToList();
        if (metaRows.Count > 2)
        {
            var values = new[]
            {
                requirement.RequirementDate.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                "10",
                "Склад металла",
                string.Empty,
                "Производство",
                string.Empty,
                "10.01",
                string.Empty,
                "шт",
            };

            SetRowCellTexts(metaRows[2], values);
        }

        var approvalRows = tables[2].Elements<TableRow>().ToList();
        if (approvalRows.Count > 0)
        {
            SetCellText(GetCell(approvalRows, 0, 1), string.Empty);
            SetCellText(GetCell(approvalRows, 0, 3), string.Empty);
        }

        var itemRows = tables[3].Elements<TableRow>().ToList();
        if (itemRows.Count <= 3)
        {
            return;
        }

        var firstDataRowIndex = 3;
        var templateRow = itemRows[firstDataRowIndex];
        var parent = templateRow.Parent;
        if (parent is null)
        {
            return;
        }

        while (itemRows.Count - firstDataRowIndex < items.Count)
        {
            var clone = (TableRow)templateRow.CloneNode(true);
            ClearRow(clone);
            parent.AppendChild(clone);
            itemRows.Add(clone);
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var materialText = item.MaterialName;

            SetRowCellTexts(itemRows[firstDataRowIndex + index], new[]
            {
                "10.01",
                string.Empty,
                materialText,
                item.MaterialCode,
                UnitCode(item.Unit),
                item.Unit,
                item.TotalRequiredQty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
            });
        }
    }

    private static void SetRowCellTexts(TableRow row, IReadOnlyList<string> values)
    {
        var cells = row.Elements<TableCell>().ToList();
        for (var index = 0; index < cells.Count && index < values.Count; index++)
        {
            SetCellText(cells[index], values[index]);
        }
    }

    private static TableCell? GetCell(IReadOnlyList<TableRow> rows, int rowIndex, int cellIndex)
    {
        if (rowIndex < 0 || rowIndex >= rows.Count)
        {
            return null;
        }

        var cells = rows[rowIndex].Elements<TableCell>().ToList();
        return cellIndex >= 0 && cellIndex < cells.Count ? cells[cellIndex] : null;
    }

    private static void ClearRow(TableRow row)
    {
        foreach (var cell in row.Elements<TableCell>())
        {
            SetCellText(cell, string.Empty);
        }
    }

    private static void SetCellText(TableCell? cell, string? value)
    {
        if (cell is null)
        {
            return;
        }

        var texts = cell.Descendants<Text>().ToList();
        if (texts.Count == 0)
        {
            cell.RemoveAllChildren<Paragraph>();
            cell.Append(new Paragraph(new Run(new Text(value ?? string.Empty))));
            return;
        }

        texts[0].Text = value ?? string.Empty;
        for (var index = 1; index < texts.Count; index++)
        {
            texts[index].Text = string.Empty;
        }
    }

    private static void ReplaceToken(OpenXmlElement root, string token, string value)
    {
        foreach (var text in root.Descendants<Text>())
        {
            if (text.Text.Contains(token, StringComparison.Ordinal))
            {
                text.Text = text.Text.Replace(token, value ?? string.Empty, StringComparison.Ordinal);
            }
        }
    }

    private static string BuildMaterial(string? name, string? code)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(code) ? name : $"{name} ({code})";
    }

    private static string Safe(string? first, string? second)
    {
        return !string.IsNullOrWhiteSpace(first)
            ? first
            : (second ?? string.Empty);
    }

    private static string UnitCode(string? unit)
    {
        return (unit ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "шт" or "pcs" => "796",
            "кг" or "kg" => "166",
            "м" or "m" => "006",
            "м2" or "м²" or "m2" => "055",
            _ => string.Empty,
        };
    }

    private static string FormatRequirementNumberForPrint(string? requirementNumber)
    {
        if (string.IsNullOrWhiteSpace(requirementNumber))
        {
            return string.Empty;
        }

        var value = requirementNumber.Trim();
        var lastDigits = Regex.Match(value, @"(?<number>\d+)\s*$", RegexOptions.CultureInvariant);
        return lastDigits.Success ? lastDigits.Groups["number"].Value : value;
    }

    private static string FormatDecimal(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string SanitizeFilePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "без_номера";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "без_номера" : sanitized;
    }

    private sealed class RequirementProjection
    {
        public Guid Id { get; init; }
        public string RequirementNumber { get; init; } = string.Empty;
        public DateTime RequirementDate { get; init; }
        public string Status { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
        public string UpdatedBy { get; init; } = string.Empty;
        public Guid WipLaunchId { get; init; }
        public DateTime? LaunchDate { get; init; }
        public string PartCode { get; init; } = string.Empty;
        public string PartName { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public List<RequirementItemProjection> Items { get; init; } = new();
    }

    private sealed class RequirementItemProjection
    {
        public string MaterialName { get; init; } = string.Empty;
        public string? MaterialCode { get; init; }
        public decimal NormPerUnit { get; init; }
        public decimal ConsumptionPerUnit { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string ConsumptionUnit { get; init; } = string.Empty;
        public decimal TotalRequiredQty { get; init; }
        public decimal RequiredQty { get; init; }
        public decimal? TotalRequiredWeightKg { get; init; }
        public decimal? RequiredWeightKg { get; init; }
        public string? SizeRaw { get; init; }
        public string? Comment { get; init; }
    }

    private sealed class RequirementPrintItem
    {
        public string RowNumber { get; init; } = string.Empty;
        public string MaterialName { get; init; } = string.Empty;
        public string Material { get; init; } = string.Empty;
        public string MaterialCode { get; init; } = string.Empty;
        public string NormPerUnit { get; init; } = string.Empty;
        public string Unit { get; init; } = string.Empty;
        public string TotalRequiredQty { get; init; } = string.Empty;
        public string TotalWeightKg { get; init; } = string.Empty;
        public string SizeOrNote { get; init; } = string.Empty;
    }
}
