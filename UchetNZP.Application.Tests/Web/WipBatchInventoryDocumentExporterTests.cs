using System;
using System.Text;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class WipBatchInventoryDocumentExporterTests
{
    [Fact]
    public void Export_BuildsTemplate_CloseToInventoryBlank()
    {
        var exporter = new WipBatchInventoryDocumentExporter();
        var model = new WipBatchReportViewModel(
            new WipBatchReportFilterViewModel
            {
                From = new DateTime(2025, 9, 1),
                To = new DateTime(2025, 9, 30),
            },
            new[]
            {
                new WipBatchReportItemViewModel(
                    "Втулка",
                    "4ЭТК.02.01.111-7",
                    "010 Термическая",
                    "040",
                    75m,
                    new DateTime(2025, 9, 30),
                    "00327: 30, 00327/1: 45"),
            },
            75m,
            Array.Empty<WipBatchInventoryDocumentListItemViewModel>());

        var bytes = exporter.Export(9, new DateTime(2025, 9, 25), new DateTime(2025, 9, 30), model);
        var html = Encoding.UTF8.GetString(bytes);

        Assert.Contains("АКТ ИНВЕНТАРИЗАЦИИ", html, StringComparison.Ordinal);
        Assert.Contains("незавершенного производства", html, StringComparison.Ordinal);
        Assert.Contains("номер</td><td class=\"right\">09/инв", html, StringComparison.Ordinal);
        Assert.Contains("Номер ярлыка (партии)", html, StringComparison.Ordinal);
        Assert.Contains("Фактическое количество на партии", html, StringComparison.Ordinal);
        Assert.Contains("00327", html, StringComparison.Ordinal);
        Assert.Contains("00327/1", html, StringComparison.Ordinal);
    }
}
