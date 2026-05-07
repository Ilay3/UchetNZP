using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Services;

public interface IMetalReceiptDocumentService
{
    Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default);
}

public sealed record MetalReceiptDocumentResult(string FileName, string ContentType, byte[] Content);

public class MetalReceiptDocumentService : IMetalReceiptDocumentService
{
    private const string TemplateRelativePath = "Templates/Documents/Приходный ордер.docx";

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public MetalReceiptDocumentService(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
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

    private static void FillHeaderAndTotals(Body body, ReceiptProjection receipt, IReadOnlyCollection<ReceiptPrintItemRow> items)
    {
        var totalWeight = items.Sum(x => ParseDecimal(x.WeightKg));
        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{receipt_date}}"] = receipt.ReceiptDate.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ["{{supplier_name}}"] = receipt.SupplierName ?? string.Empty,
            ["{{supplier_code}}"] = receipt.SupplierCode ?? string.Empty,
            ["{{supplier_document_number}}"] = receipt.SupplierDocumentNumber ?? string.Empty,
            ["{{total_weight_kg}}"] = FormatDecimal(totalWeight),
            ["{{total_amount_without_vat}}"] = FormatDecimal(receipt.TotalAmountWithoutVat),
            ["{{total_vat_amount}}"] = FormatDecimal(receipt.TotalVatAmount),
            ["{{total_amount_with_vat}}"] = FormatDecimal(receipt.TotalAmountWithVat),
        };

        foreach (var placeholder in placeholders)
        {
            ReplaceToken(body, placeholder.Key, placeholder.Value);
        }
    }

    private static void FillItemsTable(Body body, IReadOnlyCollection<ReceiptPrintItemRow> items)
    {
        var templateRow = body.Descendants<TableRow>().FirstOrDefault(row =>
            row.Descendants<Text>().Any(text =>
                text.Text.Contains("{{chek}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{item_material_name}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{item_material_code}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{item_weight_kg}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{item_price_per_kg}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{item_amount_without_vat}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{item_vat_amount}}", StringComparison.Ordinal) ||
                text.Text.Contains("{{item_total_with_vat}}", StringComparison.Ordinal)));

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
            ReplaceToken(row, "{{chek}}", item.RowNumber);
            ReplaceToken(row, "{{item_material_name}}", item.MaterialName);
            ReplaceToken(row, "{{item_material_code}}", item.MaterialCode);
            ReplaceToken(row, "{{item_weight_kg}}", item.WeightKg);
            ReplaceToken(row, "{{item_price_per_kg}}", item.PricePerKg);
            ReplaceToken(row, "{{item_amount_without_vat}}", item.AmountWithoutVat);
            ReplaceToken(row, "{{item_vat_amount}}", item.VatAmount);
            ReplaceToken(row, "{{item_total_with_vat}}", item.TotalWithVat);
            templateRow.Parent!.InsertBefore(row, templateRow);
        }

        templateRow.Remove();
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
