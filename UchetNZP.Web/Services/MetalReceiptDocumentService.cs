using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Services;

public interface IMetalReceiptDocumentService
{
    Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default);
}

public sealed record MetalReceiptDocumentResult(string FileName, string ContentType, byte[] Content);

public class MetalReceiptDocumentService : IMetalReceiptDocumentService
{
    private readonly AppDbContext _dbContext;

    public MetalReceiptDocumentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<MetalReceiptDocumentResult> BuildAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await _dbContext.MetalReceipts
            .AsNoTracking()
            .Where(x => x.Id == receiptId)
            .Select(x => new
            {
                x.ReceiptNumber,
                x.ReceiptDate,
                x.Comment,
                Items = x.Items
                    .OrderBy(i => i.ItemIndex)
                    .Select(i => new
                    {
                        i.ItemIndex,
                        MaterialName = i.MetalMaterial != null ? i.MetalMaterial.Name : string.Empty,
                        i.ActualBlankSizeText,
                        i.SizeValue,
                        i.SizeUnitText,
                        i.GeneratedCode,
                    }).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (receipt is null)
            throw new KeyNotFoundException();

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("Товарная накладная по приёму ТМЦ").Bold().FontSize(16);
                    col.Item().Text($"Документ № {receipt.ReceiptNumber} от {receipt.ReceiptDate.ToLocalTime():dd.MM.yyyy}");
                    col.Item().Text($"Склад: Металло-склад");
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("№").Bold();
                        header.Cell().Text("Материал").Bold();
                        header.Cell().Text("Размер").Bold();
                        header.Cell().Text("Ед.").Bold();
                        header.Cell().Text("Код").Bold();
                    });

                    foreach (var item in receipt.Items)
                    {
                        var sizeText = string.IsNullOrWhiteSpace(item.ActualBlankSizeText)
                            ? item.SizeValue.ToString("0.###", CultureInfo.InvariantCulture)
                            : item.ActualBlankSizeText;

                        table.Cell().Text(item.ItemIndex.ToString(CultureInfo.InvariantCulture));
                        table.Cell().Text(item.MaterialName);
                        table.Cell().Text(sizeText);
                        table.Cell().Text(item.SizeUnitText);
                        table.Cell().Text(item.GeneratedCode);
                    }
                });

                page.Footer().AlignRight().Text($"Комментарий: {receipt.Comment ?? "—"}");
            });
        }).GeneratePdf();

        return new MetalReceiptDocumentResult($"Накладная_{receipt.ReceiptNumber}.pdf", "application/pdf", pdf);
    }
}
