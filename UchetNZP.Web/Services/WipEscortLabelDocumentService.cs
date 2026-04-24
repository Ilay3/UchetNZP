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
            ["{{MAT_SIZE}}"] = receipt.MetalMaterial?.Code ?? string.Empty,
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
            ReplaceToken(row, "{{OP_NO}}", operation.Number);
            ReplaceToken(row, "{{OP_NAME}}", operation.Name);
            ClearNonOperationCells(row);
            templateRow.Parent!.InsertBefore(row, templateRow);
        }

        templateRow.Remove();
    }

    private static void ClearNonOperationCells(TableRow row)
    {
        foreach (var cell in row.Elements<TableCell>())
        {
            var containsOperationToken = cell.Descendants<Text>().Any(text =>
                text.Text.Contains("{{OP_NO}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{OP_NAME}}", StringComparison.Ordinal));

            if (containsOperationToken)
            {
                continue;
            }

            foreach (var text in cell.Descendants<Text>())
            {
                text.Text = string.Empty;
            }
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

    private sealed record EscortLabelOperationRow(string Number, string Name);
}
