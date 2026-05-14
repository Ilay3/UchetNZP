using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Web.Services;

public interface IWarehouseControlCardDocumentService
{
    Task<WarehouseControlCardDocumentResult> BuildAsync(Guid warehouseItemId, CancellationToken cancellationToken = default);
}

public sealed record WarehouseControlCardDocumentResult(string FileName, string ContentType, byte[] Content);

public class WarehouseControlCardDocumentService : IWarehouseControlCardDocumentService
{
    private const string TemplateRelativePath = "Templates/Documents/Карта контроля.docx";
    private const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public WarehouseControlCardDocumentService(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task<WarehouseControlCardDocumentResult> BuildAsync(Guid warehouseItemId, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.WarehouseItems
            .AsNoTracking()
            .Include(x => x.Part)
            .Include(x => x.AssemblyUnit)
            .Include(x => x.WarehouseLabelItems)
            .ThenInclude(x => x.WipLabel)
            .FirstOrDefaultAsync(x => x.Id == warehouseItemId, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            throw new KeyNotFoundException($"Запись склада {warehouseItemId} не найдена.");
        }

        var templatePath = Path.Combine(_environment.ContentRootPath, TemplateRelativePath);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Шаблон карты контроля не найден: {templatePath}");
        }

        await using var source = File.OpenRead(templatePath);
        using var memoryStream = new MemoryStream();
        await source.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        var card = BuildProjection(item);

        using (var document = WordprocessingDocument.Open(memoryStream, true))
        {
            var mainPart = document.MainDocumentPart;
            var mainDocument = mainPart?.Document;
            if (mainDocument?.Body is { } body)
            {
                FillControlCard(body, card);
                ApplyTimesNewRoman10(body);
                mainDocument.Save();
            }
        }

        var fileName = $"Карта_контроля_{NormalizeFileToken(card.CardNumber)}.docx";
        return new WarehouseControlCardDocumentResult(fileName, ContentType, memoryStream.ToArray());
    }

    private static WarehouseControlCardProjection BuildProjection(UchetNZP.Domain.Entities.WarehouseItem item)
    {
        var labelNumber = item.WarehouseLabelItems
            .Select(x => x.WipLabel?.Number ?? x.LabelNumber)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var cardNumber = FirstNotBlank(item.ControlCardNumber, labelNumber, item.DocumentNumber, item.Id.ToString("N")[..8]);

        return new WarehouseControlCardProjection(
            cardNumber,
            FirstNotBlank(item.DocumentNumber, cardNumber),
            item.AddedAt,
            item.Part?.Name ?? item.AssemblyUnit?.Name ?? string.Empty,
            item.Part?.Code ?? string.Empty,
            Math.Abs(item.Quantity),
            item.ControllerName ?? string.Empty,
            item.MasterName ?? string.Empty,
            item.AcceptedByName ?? string.Empty,
            item.Comment ?? string.Empty);
    }

    private static void FillControlCard(OpenXmlElement root, WarehouseControlCardProjection card)
    {
        var table = root.Descendants<Table>().FirstOrDefault();
        if (table is null)
        {
            return;
        }

        var rows = table.Elements<TableRow>().ToList();

        SetRowText(rows, 0, $"Карта контроля / сопроводительный ярлык № {card.CardNumber}");
        SetRowText(rows, 1, $"Дата {FormatDate(card.Date)}");
        SetRowText(rows, 2, $"Наименование: {card.PartName}");
        SetRowText(rows, 4, $"Чертеж №: {card.PartCode}");
        SetRowText(rows, 6, $"Кол-во: {FormatQuantity(card.Quantity)}");
        SetRowText(rows, 7, $"Контролер: {card.ControllerName}");
        SetRowText(rows, 10, $"Мастер: {card.MasterName}");
        SetRowText(rows, 11, $"Принял: {card.AcceptedByName}");

        FillMovementRow(rows, 13, card);
    }

    private static void FillMovementRow(IReadOnlyList<TableRow> rows, int index, WarehouseControlCardProjection card)
    {
        if (index < 0 || index >= rows.Count)
        {
            return;
        }

        var cells = rows[index].Elements<TableCell>().ToList();
        if (cells.Count == 0)
        {
            return;
        }

        var values = new[]
        {
            FormatQuantity(card.Quantity),
            FormatQuantity(card.Quantity),
            string.Empty,
            card.AcceptedByName,
            FormatDate(card.Date),
            card.Comment,
        };

        for (var i = 0; i < cells.Count && i < values.Length; i++)
        {
            SetCellText(cells[i], values[i]);
        }
    }

    private static void SetRowText(IReadOnlyList<TableRow> rows, int index, string value)
    {
        if (index < 0 || index >= rows.Count)
        {
            return;
        }

        var firstCell = rows[index].Elements<TableCell>().FirstOrDefault();
        if (firstCell is not null)
        {
            SetCellText(firstCell, value);
        }
    }

    private static void SetCellText(TableCell cell, string value)
    {
        var runProperties = cell
            .Descendants<RunProperties>()
            .FirstOrDefault()
            ?.CloneNode(true) as RunProperties;

        cell.RemoveAllChildren<Paragraph>();

        var run = new Run();
        if (runProperties is not null)
        {
            run.RunProperties = runProperties;
        }

        run.Append(new Text(value ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
        cell.Append(new Paragraph(run));
    }

    private static void ApplyTimesNewRoman10(OpenXmlElement root)
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
            runProperties.FontSize = new FontSize { Val = "20" };
            runProperties.FontSizeComplexScript = new FontSizeComplexScript { Val = "20" };
        }
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
    }

    private static string FormatQuantity(decimal quantity)
    {
        return $"{quantity.ToString("0.###", CultureInfo.GetCultureInfo("ru-RU"))} шт";
    }

    private static string FirstNotBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string NormalizeFileToken(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "без_номера" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            normalized = normalized.Replace(invalid, '_');
        }

        return normalized.Replace(' ', '_');
    }

    private sealed record WarehouseControlCardProjection(
        string CardNumber,
        string DocumentNumber,
        DateTime Date,
        string PartName,
        string PartCode,
        decimal Quantity,
        string ControllerName,
        string MasterName,
        string AcceptedByName,
        string Comment);
}
