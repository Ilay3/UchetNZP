using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;

namespace UchetNZP.Web.Services;

public interface IWipEscortLabelDocumentService
{
    Task<byte[]> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default);
}

public class WipEscortLabelDocumentService : IWipEscortLabelDocumentService
{
    private const string TemplateRelativePath = "Templates/Documents/Сопроводительный ярлык.docx";
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public WipEscortLabelDocumentService(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task<byte[]> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await _dbContext.WipReceipts
            .AsNoTracking()
            .Include(x => x.Part)
            .Include(x => x.MetalMaterial)
            .Include(x => x.WipLabel)
            .FirstOrDefaultAsync(x => x.Id == receiptId, cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            throw new KeyNotFoundException($"Приход {receiptId} не найден.");
        }

        var operations = await _dbContext.PartRoutes
            .AsNoTracking()
            .Where(x => x.PartId == receipt.PartId)
            .Include(x => x.Operation)
            .OrderBy(x => x.OpNumber)
            .Select(x => new EscortLabelOperationRow(
                OperationNumber.Format(x.OpNumber),
                x.Operation != null ? x.Operation.Name : string.Empty))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var materialSizeText = await ResolveMaterialSizeTextAsync(receipt.MetalMaterialId, cancellationToken)
            .ConfigureAwait(false);

        var templatePath = Path.Combine(_environment.ContentRootPath, TemplateRelativePath);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Шаблон документа не найден: {templatePath}");
        }

        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{LBL}}"] = receipt.WipLabel != null ? receipt.WipLabel.Number : string.Empty,
            ["{{PART}}"] = receipt.Part?.Name ?? string.Empty,
            ["{{DRAW}}"] = receipt.Part?.Code ?? string.Empty,
            ["{{TP}}"] = string.Empty,
            ["{{QTY}}"] = receipt.Quantity.ToString("0.###"),
            ["{{MAT}}"] = receipt.MetalMaterial?.Name ?? string.Empty,
            ["{{MAT_QTY}}"] = receipt.Quantity.ToString("0.###"),
            ["{{MAT_SIZE}}"] = materialSizeText,
            ["{{CERT}}"] = string.Empty,
            ["{{COMP_NO}}"] = string.Empty,
            ["{{COMP_QTY}}"] = string.Empty,
        };

        await using var source = File.OpenRead(templatePath);
        using var memoryStream = new MemoryStream();
        await source.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        using (var document = WordprocessingDocument.Open(memoryStream, true))
        {
            var body = document.MainDocumentPart?.Document.Body;
            if (body is null)
            {
                return memoryStream.ToArray();
            }

            foreach (var token in placeholders)
            {
                ReplaceToken(body, token.Key, token.Value);
            }

            FillOperationsTable(body, operations);
            ApplyTimesNewRoman8(body);
            document.MainDocumentPart!.Document.Save();
        }

        return memoryStream.ToArray();
    }

    private static void FillOperationsTable(OpenXmlElement root, IReadOnlyList<EscortLabelOperationRow> operations)
    {
        var templateRow = root
            .Descendants<TableRow>()
            .FirstOrDefault(row => row.Descendants<Text>().Any(text =>
                text.Text.Contains("{{OP_NO}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{OP_NAME}}", StringComparison.Ordinal)));

        if (templateRow is null)
        {
            return;
        }

        if (operations.Count == 0)
        {
            templateRow.Remove();
            return;
        }

        foreach (var operation in operations)
        {
            var row = (TableRow)templateRow.CloneNode(true);
            FillOperationRow(row, operation);
            templateRow.Parent!.InsertBefore(row, templateRow);
        }

        templateRow.Remove();
    }

    private static void FillOperationRow(TableRow row, EscortLabelOperationRow operation)
    {
        var cells = row.Elements<TableCell>().ToList();
        if (cells.Count == 0)
        {
            return;
        }

        var numberColumnIndex = cells.Count >= 3 ? 1 : 0;
        var nameColumnIndex = cells.Count >= 3 ? 2 : 1;
        SetCellText(cells[numberColumnIndex], operation.Number);
        if (cells.Count > nameColumnIndex)
        {
            SetCellText(cells[nameColumnIndex], operation.Name);
        }

        for (var i = 0; i < cells.Count; i++)
        {
            if (i == numberColumnIndex || i == nameColumnIndex)
            {
                continue;
            }

            SetCellText(cells[i], string.Empty);
        }
    }

    private static void SetCellText(TableCell cell, string value)
    {
        var texts = cell.Descendants<Text>().ToList();
        if (texts.Count == 0)
        {
            cell.RemoveAllChildren<Paragraph>();
            cell.Append(new Paragraph(new Run(new Text(value))));
            return;
        }

        texts[0].Text = value;
        for (var i = 1; i < texts.Count; i++)
        {
            texts[i].Text = string.Empty;
        }
    }

    private async Task<string> ResolveMaterialSizeTextAsync(Guid? metalMaterialId, CancellationToken cancellationToken)
    {
        if (!metalMaterialId.HasValue)
        {
            return string.Empty;
        }

        var materialSize = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Where(x => x.MetalMaterialId == metalMaterialId.Value)
            .OrderByDescending(x => x.MetalReceipt != null ? x.MetalReceipt.ReceiptDate : DateTime.MinValue)
            .ThenByDescending(x => x.ItemIndex)
            .Select(x => new
            {
                x.SizeValue,
                x.SizeUnitText,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (materialSize is null)
        {
            return string.Empty;
        }

        var sizeValueText = materialSize.SizeValue.ToString("0.###");
        return string.IsNullOrWhiteSpace(materialSize.SizeUnitText)
            ? sizeValueText
            : $"{sizeValueText} {materialSize.SizeUnitText}";
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

    private static void ApplyTimesNewRoman8(OpenXmlElement root)
    {
        foreach (var run in root.Descendants<Run>())
        {
            var runProperties = run.GetFirstChild<RunProperties>();
            if (runProperties is null)
            {
                runProperties = new RunProperties();
                run.PrependChild(runProperties);
            }

            runProperties.RunFonts = new RunFonts
            {
                Ascii = "Times New Roman",
                HighAnsi = "Times New Roman",
                ComplexScript = "Times New Roman",
            };
            runProperties.FontSize = new FontSize { Val = "16" };
            runProperties.FontSizeComplexScript = new FontSizeComplexScript { Val = "16" };
        }
    }

    private sealed record EscortLabelOperationRow(string Number, string Name);
}
