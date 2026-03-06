using System;
using System.Text;
using QuestPDF.Infrastructure;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class WipBatchInventoryDocumentExporterTests
{
    [Fact]
    public void Export_BuildsPdfDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community;
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
        Assert.True(bytes.Length > 4);
        var signature = Encoding.ASCII.GetString(bytes, 0, 4);
        Assert.Equal("%PDF", signature);
    }
}
