using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Services;

public interface IMetalReceiptDocumentService
{
    Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default);
    Task<MetalReceiptDocumentResult> BuildPdfAsync(Guid receiptId, CancellationToken cancellationToken = default);
}

public sealed record MetalReceiptDocumentResult(string FileName, string ContentType, byte[] Content);

public class MetalReceiptDocumentService : IMetalReceiptDocumentService
{
    private const string TemplateRelativePath = "Templates/Documents/Приходный ордер.docx";

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public MetalReceiptDocumentService(AppDbContext dbContext, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _environment = environment;
        _configuration = configuration;
    }

    public async Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await _dbContext.MetalReceipts
            .AsNoTracking()
            .Where(x => x.Id == receiptId)
            .Select(x => new ReceiptProjection
            {
                ReceiptNumber = x.ReceiptNumber,
                ReceiptDate = x.ReceiptDate,
                SupplierName = x.SupplierNameSnapshot ?? (x.MetalSupplier != null ? x.MetalSupplier.Name : null) ?? x.SupplierOrSource,
                SupplierCode = x.SupplierIdentifierSnapshot ?? (x.MetalSupplier != null ? x.MetalSupplier.Identifier : null) ?? x.SupplierInnSnapshot,
                SupplierDocumentNumber = x.SupplierDocumentNumber,
                TotalAmountWithoutVat = x.AmountWithoutVat,
                TotalVatAmount = x.VatAmount,
                TotalAmountWithVat = x.TotalAmountWithVat,
                Items = x.Items
                    .OrderBy(i => i.ReceiptLineIndex)
                    .ThenBy(i => i.ItemIndex)
                    .ThenBy(i => i.CreatedAt)
                    .Select(i => new ReceiptItemProjection
                    {
                        MaterialName = i.MetalMaterial != null ? i.MetalMaterial.Name : string.Empty,
                        MaterialCode = i.MetalMaterial != null ? i.MetalMaterial.Code : string.Empty,
                        WeightKg = i.TotalWeightKg,
                        PricePerKg = i.PricePerKg,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (receipt is null)
            throw new KeyNotFoundException();

        var templatePath = Path.Combine(_environment.ContentRootPath, TemplateRelativePath);
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Шаблон печатной формы приходного ордера не найден в проекте.", templatePath);

        await using var templateStream = File.OpenRead(templatePath);
        using var memoryStream = new MemoryStream();
        await templateStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        var itemRows = receipt.Items.Select((item, index) =>
        {
            var amountWithoutVat = item.WeightKg * item.PricePerKg;
            var vatAmount = amountWithoutVat * 0.22m;
            return new ReceiptPrintItemRow
            {
                RowNumber = (index + 1).ToString(CultureInfo.InvariantCulture),
                MaterialName = item.MaterialName ?? string.Empty,
                MaterialCode = item.MaterialCode ?? string.Empty,
                WeightKg = FormatDecimal(item.WeightKg),
                PricePerKg = FormatDecimal(item.PricePerKg),
                AmountWithoutVat = FormatDecimal(amountWithoutVat),
                VatAmount = FormatDecimal(vatAmount),
                TotalWithVat = FormatDecimal(amountWithoutVat + vatAmount),
            };
        }).ToList();

        using (var document = WordprocessingDocument.Open(memoryStream, true))
        {
            var body = document.MainDocumentPart?.Document?.Body;
            if (body is not null)
            {
                FillHeaderAndTotals(body, receipt, itemRows);
                FillItemsTable(body, itemRows);
                document.MainDocumentPart?.Document?.Save();
            }
        }

        var fileName = $"Приходный_ордер_{receipt.ReceiptNumber}.docx";
        const string contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        return new MetalReceiptDocumentResult(fileName, contentType, memoryStream.ToArray());
    }

    public async Task<MetalReceiptDocumentResult> BuildPdfAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var docx = await BuildAsync(receiptId, cancellationToken);
        var sofficePath = _configuration["LibreOffice:ExecutablePath"];
        if (string.IsNullOrWhiteSpace(sofficePath) || !File.Exists(sofficePath))
            throw new FileNotFoundException("Не найден путь к LibreOffice (LibreOffice:ExecutablePath).", sofficePath);

        var tempRoot = Path.Combine(Path.GetTempPath(), "uchetnzp-receipts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var docxPath = Path.Combine(tempRoot, docx.FileName);
            await File.WriteAllBytesAsync(docxPath, docx.Content, cancellationToken);

            var psi = new ProcessStartInfo
            {
                FileName = sofficePath,
                Arguments = $"--headless --convert-to pdf --outdir \"{tempRoot}\" \"{docxPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить LibreOffice.");
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                var err = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException($"LibreOffice завершился с ошибкой: {err}");
            }

            var pdfPath = Path.ChangeExtension(docxPath, ".pdf");
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("LibreOffice не создал PDF файл.", pdfPath);

            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken);
            return new MetalReceiptDocumentResult(Path.GetFileName(pdfPath), "application/pdf", pdfBytes);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    private static void FillHeaderAndTotals(
    Body body,
    ReceiptProjection receipt,
    IReadOnlyCollection<ReceiptPrintItemRow> items)
    {
        var totalWeight = items.Sum(x => ParseDecimal(x.WeightKg));

        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{receipt_number}}"] = FormatReceiptNumberForPrint(receipt.ReceiptNumber),
            ["{{receipt_date}}"] = receipt.ReceiptDate.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ["{{supplier_name}}"] = receipt.SupplierName ?? string.Empty,
            ["{{supplier_code}}"] = receipt.SupplierCode ?? string.Empty,
            ["{{supplier_document_number}}"] = receipt.SupplierDocumentNumber ?? string.Empty,
            ["{{total_weight_kg}}"] = FormatDecimal(totalWeight),
            ["{{total_amount_without_vat}}"] = FormatDecimal(receipt.TotalAmountWithoutVat),
            ["{{total_vat_amount}}"] = FormatDecimal(receipt.TotalVatAmount),
            ["{{total_amount_with_vat}}"] = FormatDecimal(receipt.TotalAmountWithVat),
        };

        ReplaceTokens(body, placeholders);
    }

    private static string FormatReceiptNumberForPrint(string receiptNumber)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
            return string.Empty;

        var value = receiptNumber.Trim();

        // Пример: ПРИХОД МЕТАЛЛА-20260508-№0013 -> 0013
        var numberAfterSignMatch = Regex.Match(
            value,
            @"(?:№|#)\s*(?<number>\d+)\s*$",
            RegexOptions.CultureInvariant);

        if (numberAfterSignMatch.Success)
            return numberAfterSignMatch.Groups["number"].Value;

        // Пример: ПРИХОД-МЕТАЛЛА-20260508-0013 -> 0013
        var lastDigitsMatch = Regex.Match(
            value,
            @"(?<number>\d+)\s*$",
            RegexOptions.CultureInvariant);

        if (lastDigitsMatch.Success)
            return lastDigitsMatch.Groups["number"].Value;

        return value;
    }

    private static void FillItemsTable(Body body, IReadOnlyCollection<ReceiptPrintItemRow> items)
    {
        var templateRow = body.Descendants<TableRow>().FirstOrDefault(row =>
            RowContainsAnyToken(
                row,
                "{{chek}}",
                "{{check}}",
                "{{item_material_name}}",
                "{{item_material_code}}",
                "{{item_weight_kg}}",
                "{{item_price_per_kg}}",
                "{{item_amount_without_vat}}",
                "{{item_vat_amount}}",
                "{{item_total_with_vat}}"));

        if (templateRow is null)
            return;

        if (items.Count == 0)
        {
            templateRow.Remove();
            return;
        }

        foreach (var item in items)
        {
            var row = (TableRow)templateRow.CloneNode(true);

            var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["{{chek}}"] = item.RowNumber,
                ["{{check}}"] = item.RowNumber,
                ["{{item_material_name}}"] = item.MaterialName,
                ["{{item_material_code}}"] = item.MaterialCode,
                ["{{item_weight_kg}}"] = item.WeightKg,
                ["{{item_price_per_kg}}"] = item.PricePerKg,
                ["{{item_amount_without_vat}}"] = item.AmountWithoutVat,
                ["{{item_vat_amount}}"] = item.VatAmount,
                ["{{item_total_with_vat}}"] = item.TotalWithVat,
            };

            ReplaceTokens(row, placeholders);

            templateRow.Parent!.InsertBefore(row, templateRow);
        }

        templateRow.Remove();
    }

    private static bool RowContainsAnyToken(TableRow row, params string[] tokens)
    {
        var rowText = string.Concat(row.Descendants<Text>().Select(x => x.Text));
        var normalizedRowText = NormalizeTemplateTokens(rowText);
        return tokens.Any(token => normalizedRowText.Contains(token, StringComparison.Ordinal));
    }

    private static void ReplaceTokens(OpenXmlElement root, IReadOnlyDictionary<string, string> tokens)
    {
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            ReplaceTokensInTextContainer(paragraph, tokens);
        }
    }

    private static void ReplaceTokensInTextContainer(
        OpenXmlElement container,
        IReadOnlyDictionary<string, string> tokens)
    {
        var textNodes = container.Descendants<Text>().ToList();

        if (textNodes.Count == 0)
            return;

        var originalText = string.Concat(textNodes.Select(x => x.Text));
        var normalizedText = NormalizeTemplateTokens(originalText);

        var replacedText = normalizedText;

        foreach (var token in tokens)
        {
            var normalizedToken = NormalizeTemplateTokens(token.Key);

            replacedText = replacedText.Replace(
                normalizedToken,
                token.Value ?? string.Empty,
                StringComparison.Ordinal);
        }

        if (replacedText == normalizedText)
            return;

        textNodes[0].Text = replacedText;

        for (var i = 1; i < textNodes.Count; i++)
        {
            textNodes[i].Text = string.Empty;
        }
    }

    private static string NormalizeTemplateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return Regex.Replace(
            text,
            @"\{\{(?<inner>.*?)\}\}",
            match =>
            {
                var inner = match.Groups["inner"].Value;

                var compactInner = string.Concat(
                    inner.Where(c =>
                        !char.IsWhiteSpace(c)
                        && c != '\u00A0'
                        && c != '\u200B'
                        && c != '\u200C'
                        && c != '\u200D'
                        && c != '\uFEFF'));

                return $"{{{{{compactInner}}}}}";
            },
            RegexOptions.Singleline);
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    private sealed class ReceiptProjection
    {
        public string ReceiptNumber { get; init; } = string.Empty;
        public DateTime ReceiptDate { get; init; }
        public string? SupplierName { get; init; }
        public string? SupplierCode { get; init; }
        public string? SupplierDocumentNumber { get; init; }
        public decimal TotalAmountWithoutVat { get; init; }
        public decimal TotalVatAmount { get; init; }
        public decimal TotalAmountWithVat { get; init; }
        public List<ReceiptItemProjection> Items { get; init; } = new();
    }

    private sealed class ReceiptItemProjection
    {
        public string? MaterialName { get; init; }
        public string? MaterialCode { get; init; }
        public decimal WeightKg { get; init; }
        public decimal PricePerKg { get; init; }
    }

    private sealed class ReceiptPrintItemRow
    {
        public string RowNumber { get; init; } = string.Empty;
        public string MaterialName { get; init; } = string.Empty;
        public string MaterialCode { get; init; } = string.Empty;
        public string WeightKg { get; init; } = string.Empty;
        public string PricePerKg { get; init; } = string.Empty;
        public string AmountWithoutVat { get; init; } = string.Empty; 
        public string VatAmount { get; init; } = string.Empty;
        public string TotalWithVat { get; init; } = string.Empty;
    }
}
