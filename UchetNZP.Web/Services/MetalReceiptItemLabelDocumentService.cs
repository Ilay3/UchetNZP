using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Services;

public interface IMetalReceiptItemLabelDocumentService
{
    Task<MetalReceiptItemLabelDocumentResult> BuildAsync(Guid receiptItemId, string qrTarget, CancellationToken cancellationToken = default);
}

public sealed record MetalReceiptItemLabelDocumentResult(string FileName, string ContentType, byte[] Content, string QrPayload);

public class MetalReceiptItemLabelDocumentService : IMetalReceiptItemLabelDocumentService
{
    private const float LabelWidthMm = 50f;
    private const float LabelHeightMm = 30f;

    private readonly AppDbContext _dbContext;

    public MetalReceiptItemLabelDocumentService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<MetalReceiptItemLabelDocumentResult> BuildAsync(Guid receiptItemId, string qrTarget, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrTarget))
        {
            throw new ArgumentException("Целевой URL/payload для QR обязателен.", nameof(qrTarget));
        }

        var item = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .Where(x => x.Id == receiptItemId)
            .Select(x => new
            {
                x.Id,
                x.GeneratedCode,
                x.SizeValue,
                x.SizeUnitText,
                x.ActualBlankSizeText,
                MaterialName = x.MetalMaterial != null ? x.MetalMaterial.Name : string.Empty,
                MaterialCode = x.MetalMaterial != null ? x.MetalMaterial.Code : string.Empty,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            throw new KeyNotFoundException($"Единица прихода {receiptItemId} не найдена.");
        }

        var displaySize = ResolveDisplaySize(item.SizeValue, item.SizeUnitText, item.ActualBlankSizeText);
        var qrPng = BuildQrCodePng(qrTarget);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(LabelWidthMm, LabelHeightMm, Unit.Millimetre);
                page.Margin(2, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(7));
                page.Content().Row(row =>
                {
                    row.RelativeItem(2.2f).Column(col =>
                    {
                        col.Item().Text(item.MaterialName).Bold().FontSize(9);
                        col.Item().PaddingTop(1).Text($"Размер: {displaySize}").FontSize(7);
                        if (!string.IsNullOrWhiteSpace(item.MaterialCode))
                        {
                            col.Item().Text($"Код: {item.MaterialCode}").FontSize(7);
                        }

                        if (!string.IsNullOrWhiteSpace(item.GeneratedCode))
                        {
                            col.Item().Text($"Ед.: {item.GeneratedCode}").FontSize(6);
                        }
                    });

                    row.ConstantItem(22, Unit.Millimetre)
                        .Height(22, Unit.Millimetre)
                        .Image(qrPng);
                });
            });
        }).GeneratePdf();

        var fileName = $"Этикетка_{SanitizeFileNamePart(item.GeneratedCode)}.pdf";
        return new MetalReceiptItemLabelDocumentResult(fileName, "application/pdf", pdf, qrTarget);
    }

    private static byte[] BuildQrCodePng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrData);
        return pngQrCode.GetGraphic(12);
    }

    private static string ResolveDisplaySize(decimal sizeValue, string? sizeUnitText, string? actualBlankSizeText)
    {
        if (!string.IsNullOrWhiteSpace(actualBlankSizeText))
        {
            return actualBlankSizeText.Trim();
        }

        var value = sizeValue.ToString("0.###", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(sizeUnitText)
            ? value
            : $"{value} {sizeUnitText}";
    }

    private static string SanitizeFileNamePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "metal_item";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "metal_item" : sanitized;
    }
}
