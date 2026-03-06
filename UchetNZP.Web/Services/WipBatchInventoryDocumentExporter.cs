using System.Globalization;
using System.Net;
using System.Text;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Services;

public interface IWipBatchInventoryDocumentExporter
{
    byte[] Export(int inventoryNumber, DateTime generatedAt, DateTime composedAt, WipBatchReportViewModel model);
}

public class WipBatchInventoryDocumentExporter : IWipBatchInventoryDocumentExporter
{
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public byte[] Export(int inventoryNumber, DateTime generatedAt, DateTime composedAt, WipBatchReportViewModel model)
    {
        var sb = new StringBuilder();
        var inventory = inventoryNumber.ToString("00", CultureInfo.InvariantCulture);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ru\"><head><meta charset=\"utf-8\" /><title>Акт инвентаризации НЗП</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Times New Roman',serif;font-size:18px;line-height:1.2;color:#000;max-width:1120px;margin:0 auto;padding:24px 28px;}");
        sb.AppendLine(".header-org{text-align:center;font-size:38px;line-height:1.1;margin-bottom:2px;}");
        sb.AppendLine(".header-note{text-align:center;font-size:28px;margin-bottom:28px;}");
        sb.AppendLine(".h-line{border-top:1px solid #000;margin-bottom:14px;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;}");
        sb.AppendLine(".meta{width:360px;margin-left:auto;margin-bottom:12px;font-size:34px;}");
        sb.AppendLine(".meta td{border:1px solid #000;padding:3px 8px;}");
        sb.AppendLine(".meta td:first-child{border:none;text-align:right;padding-right:12px;}");
        sb.AppendLine(".meta2{width:360px;margin-left:auto;margin-bottom:20px;font-size:34px;}");
        sb.AppendLine(".meta2 th,.meta2 td{border:1px solid #000;padding:3px 8px;text-align:center;}");
        sb.AppendLine(".title{text-align:center;font-size:44px;font-weight:700;margin:22px 0 0;}");
        sb.AppendLine(".subtitle{text-align:center;font-size:42px;font-weight:700;margin:0 0 22px;}");
        sb.AppendLine(".text{font-size:40px;margin:0 0 10px;}");
        sb.AppendLine(".act{font-size:30px;}");
        sb.AppendLine(".act th,.act td{border:1px solid #000;padding:4px 6px;vertical-align:top;}");
        sb.AppendLine(".act th{font-weight:400;text-align:center;}");
        sb.AppendLine(".right{text-align:right;}");
        sb.AppendLine(".center{text-align:center;}");
        sb.AppendLine(".w-num{width:62px;}.w-part{width:300px;}.w-total{width:180px;}.w-op{width:260px;}.w-label{width:180px;}.w-labelqty{width:230px;}");
        sb.AppendLine(".col-index td{padding:2px 6px;text-align:center;}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<div class=\"header-org\">ООО \"Промавтоматика\"</div>");
        sb.AppendLine("<div class=\"h-line\"></div>");
        sb.AppendLine("<div class=\"header-note\">организация</div>");

        sb.AppendLine("<table class=\"meta\">");
        sb.AppendLine($"<tr><td>номер</td><td class=\"right\">{inventory}/инв</td></tr>");
        sb.AppendLine($"<tr><td>дата</td><td class=\"right\">{generatedAt:dd.MM.yyyy}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<table class=\"meta2\">");
        sb.AppendLine("<tr><th>N</th><th>Дата составления</th></tr>");
        sb.AppendLine($"<tr><td>{inventory}</td><td>{composedAt:dd.MM.yyyy}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class=\"title\">АКТ ИНВЕНТАРИЗАЦИИ</div>");
        sb.AppendLine("<div class=\"subtitle\">незавершенного производства</div>");

        sb.AppendLine($"<p class=\"text\">Акт о том, что по состоянию на <strong>{ToLongRussianDate(composedAt)}</strong> проведена инвентаризация незавершенного производства.</p>");
        sb.AppendLine("<p class=\"text\">При инвентаризации установлено следующее:</p>");

        sb.AppendLine("<table class=\"act\">");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th class=\"w-num\" rowspan=\"3\">N<br/>п/п</th>");
        sb.AppendLine("<th class=\"w-part\" rowspan=\"3\">Наименование детали в производстве</th>");
        sb.AppendLine("<th class=\"w-total\" rowspan=\"3\">Фактическое количество НЗП, шт.</th>");
        sb.AppendLine("<th class=\"w-op\" rowspan=\"2\">Операция</th>");
        sb.AppendLine("<th colspan=\"2\">Подробная информация</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th colspan=\"2\">нарастающим итогом</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th>4</th>");
        sb.AppendLine("<th class=\"w-label\">Номер ярлыка (партии)</th>");
        sb.AppendLine("<th class=\"w-labelqty\">Фактическое количество на партии</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("<tr class=\"col-index\"><td>1</td><td>2</td><td>3</td><td>4</td><td>5</td><td>6</td></tr>");
        sb.AppendLine("</thead><tbody>");

        if (model.Items.Count == 0)
        {
            sb.AppendLine("<tr><td colspan=\"6\" class=\"center\">Нет данных по выбранным условиям</td></tr>");
        }
        else
        {
            var rowNo = 1;
            foreach (var item in model.Items)
            {
                var labelRows = ParseLabelRows(item.LabelNumbers, item.Quantity);
                var first = true;
                foreach (var labelRow in labelRows)
                {
                    sb.Append("<tr>");
                    if (first)
                    {
                        sb.Append($"<td>{rowNo}</td>");
                        sb.Append("<td>" + HtmlPart(item) + "</td>");
                        sb.Append($"<td class=\"center\"><strong>{item.Quantity:0.###}</strong></td>");
                        sb.Append("<td>" + WebUtility.HtmlEncode(item.OpNumber + " " + item.SectionName) + "</td>");
                        first = false;
                    }
                    else
                    {
                        sb.Append("<td></td><td></td><td></td><td></td>");
                    }

                    sb.Append("<td class=\"center\">" + WebUtility.HtmlEncode(labelRow.LabelNumber) + "</td>");
                    sb.Append($"<td class=\"center\">{labelRow.Quantity:0.###}</td>");
                    sb.AppendLine("</tr>");
                }

                rowNo++;
            }
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p class=\"text\"><strong>Итого:</strong> {model.TotalQuantity:0.###} шт.</p>");
        sb.AppendLine("</body></html>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string ToLongRussianDate(DateTime date)
    {
        return $"{date.Day} {date.ToString("MMMM", RuCulture)} {date.Year} г.";
    }

    private static string HtmlPart(WipBatchReportItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(item.PartCode))
        {
            return WebUtility.HtmlEncode(item.PartName);
        }

        return WebUtility.HtmlEncode(item.PartName) + "<br/>" + WebUtility.HtmlEncode(item.PartCode);
    }

    private static IReadOnlyList<(string LabelNumber, decimal Quantity)> ParseLabelRows(string? labels, decimal fallbackQuantity)
    {
        if (string.IsNullOrWhiteSpace(labels))
        {
            return new[] { ("—", fallbackQuantity) };
        }

        var items = labels
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToList();

        if (items.Count == 0)
        {
            return new[] { ("—", fallbackQuantity) };
        }

        var result = new List<(string LabelNumber, decimal Quantity)>(items.Count);
        foreach (var raw in items)
        {
            var sep = raw.LastIndexOf(':');
            if (sep <= 0 || sep == raw.Length - 1)
            {
                result.Add((raw, fallbackQuantity));
                continue;
            }

            var label = raw[..sep].Trim();
            var qtyText = raw[(sep + 1)..].Trim();
            if (decimal.TryParse(qtyText, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)
                || decimal.TryParse(qtyText, NumberStyles.Number, RuCulture, out qty))
            {
                result.Add((string.IsNullOrWhiteSpace(label) ? "—" : label, qty));
            }
            else
            {
                result.Add((string.IsNullOrWhiteSpace(label) ? raw : label, fallbackQuantity));
            }
        }

        return result.Count == 0 ? new[] { ("—", fallbackQuantity) } : result;
    }
}
