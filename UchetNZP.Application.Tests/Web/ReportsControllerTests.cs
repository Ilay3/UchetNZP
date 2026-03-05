using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Controllers;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class ReportsControllerTests
{
    [Fact]
    public async Task WipBatchReport_ShowsResidualSlashLabelNumbers()
    {
        await using var dbContext = CreateContext();

        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var originalLabelId = Guid.NewGuid();
        var residualLabelId = Guid.NewGuid();

        dbContext.Parts.Add(new Part { Id = partId, Name = "Втулка", Code = "4ЭТК.02.01.111-7" });
        dbContext.Sections.Add(new Section { Id = sectionId, Name = "Заготовительная" });

        dbContext.WipLabels.AddRange(
            new WipLabel
            {
                Id = originalLabelId,
                PartId = partId,
                LabelDate = DateTime.SpecifyKind(new DateTime(2026, 3, 2), DateTimeKind.Unspecified),
                Quantity = 25m,
                RemainingQuantity = 0m,
                Number = "00001",
                IsAssigned = true,
            },
            new WipLabel
            {
                Id = residualLabelId,
                PartId = partId,
                LabelDate = DateTime.SpecifyKind(new DateTime(2026, 3, 2), DateTimeKind.Unspecified),
                Quantity = 10m,
                RemainingQuantity = 10m,
                Number = "00001/1",
                IsAssigned = true,
            });

        dbContext.WipReceipts.Add(new WipReceipt
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 5,
            ReceiptDate = DateTime.SpecifyKind(new DateTime(2026, 3, 2), DateTimeKind.Utc),
            Quantity = 25m,
            WipLabelId = originalLabelId,
        });

        dbContext.TransferAudits.Add(new TransferAudit
        {
            Id = Guid.NewGuid(),
            TransferDate = DateTime.SpecifyKind(new DateTime(2026, 3, 2), DateTimeKind.Utc),
            PartId = partId,
            FromSectionId = sectionId,
            FromOpNumber = 5,
            ToSectionId = sectionId,
            ToOpNumber = 10,
            Quantity = 15m,
            ScrapQuantity = 0m,
            IsWarehouseTransfer = false,
            WipLabelId = originalLabelId,
            ResidualWipLabelId = residualLabelId,
            ResidualLabelQuantity = 10m,
            IsReverted = false,
        });

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 5,
            Quantity = 10m,
        });

        await dbContext.SaveChangesAsync();

        var controller = new ReportsController(
            dbContext,
            new StubScrapExporter(),
            new StubTransferExporter(),
            new StubWipBatchExporter(),
            new StubScrapPdfExporter(),
            new StubTransferPdfExporter(),
            new StubWipBatchPdfExporter(),
            new WipLabelLookupService(dbContext));

        var result = await controller.WipBatchReport(null, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WipBatchReportViewModel>(viewResult.Model);
        var row = Assert.Single(model.Items);

        Assert.NotNull(row.LabelNumbers);
        Assert.Contains("00001/1: 10", row.LabelNumbers!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransferPeriodReport_IncludesResidualSlashLabelInCellText()
    {
        await using var dbContext = CreateContext();

        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var originalLabelId = Guid.NewGuid();

        dbContext.Parts.Add(new Part { Id = partId, Name = "Втулка", Code = "4ЭТК.02.01.111-7" });
        dbContext.Sections.Add(new Section { Id = sectionId, Name = "Заготовительная" });
        dbContext.WipLabels.Add(new WipLabel
        {
            Id = originalLabelId,
            PartId = partId,
            LabelDate = DateTime.SpecifyKind(new DateTime(2026, 3, 2), DateTimeKind.Unspecified),
            Quantity = 25m,
            RemainingQuantity = 0m,
            Number = "00001",
            IsAssigned = true,
        });

        var transferId = Guid.NewGuid();
        var transferDate = DateTime.SpecifyKind(new DateTime(2026, 3, 2, 8, 0, 0), DateTimeKind.Utc);

        dbContext.WipTransfers.Add(new WipTransfer
        {
            Id = transferId,
            UserId = Guid.NewGuid(),
            PartId = partId,
            FromSectionId = sectionId,
            FromOpNumber = 5,
            ToSectionId = sectionId,
            ToOpNumber = 10,
            TransferDate = transferDate,
            CreatedAt = transferDate,
            Quantity = 15m,
            WipLabelId = originalLabelId,
        });

        dbContext.TransferAudits.Add(new TransferAudit
        {
            Id = Guid.NewGuid(),
            TransactionId = Guid.NewGuid(),
            TransferId = transferId,
            TransferDate = transferDate,
            CreatedAt = transferDate,
            UserId = Guid.NewGuid(),
            PartId = partId,
            FromSectionId = sectionId,
            FromOpNumber = 5,
            ToSectionId = sectionId,
            ToOpNumber = 10,
            Quantity = 15m,
            ScrapQuantity = 0m,
            IsWarehouseTransfer = false,
            WipLabelId = originalLabelId,
            ResidualLabelNumber = "00001/1",
            ResidualLabelQuantity = 10m,
            IsReverted = false,
        });

        await dbContext.SaveChangesAsync();

        var controller = new ReportsController(
            dbContext,
            new StubScrapExporter(),
            new StubTransferExporter(),
            new StubWipBatchExporter(),
            new StubScrapPdfExporter(),
            new StubTransferPdfExporter(),
            new StubWipBatchPdfExporter(),
            new WipLabelLookupService(dbContext));

        var queryDate = DateTime.SpecifyKind(new DateTime(2026, 3, 2), DateTimeKind.Unspecified);
        var result = await controller.TransferPeriodReport(
            new ReportsController.TransferPeriodReportQuery(queryDate, queryDate, null, null),
            CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TransferPeriodReportViewModel>(viewResult.Model);
        var row = Assert.Single(model.Items);
        var day = DateTime.SpecifyKind(new DateTime(2026, 3, 2), DateTimeKind.Unspecified);
        var cellText = Assert.Single(row.Cells[day]);

        Assert.Contains("00001/1", cellText, StringComparison.Ordinal);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class StubScrapExporter : IScrapReportExcelExporter
    {
        public byte[] Export(ScrapReportFilterViewModel in_filter, System.Collections.Generic.IReadOnlyList<ScrapReportItemViewModel> in_items)
            => Array.Empty<byte>();
    }

    private sealed class StubTransferExporter : ITransferPeriodReportExcelExporter
    {
        public byte[] Export(TransferPeriodReportFilterViewModel in_filter, System.Collections.Generic.IReadOnlyList<DateTime> in_dates, System.Collections.Generic.IReadOnlyList<TransferPeriodReportItemViewModel> in_items)
            => Array.Empty<byte>();
    }

    private sealed class StubWipBatchExporter : IWipBatchReportExcelExporter
    {
        public byte[] Export(WipBatchReportFilterViewModel filter, System.Collections.Generic.IReadOnlyList<WipBatchReportItemViewModel> items, decimal totalQuantity)
            => Array.Empty<byte>();
    }


    private sealed class StubScrapPdfExporter : IScrapReportPdfExporter
    {
        public byte[] Export(ScrapReportFilterViewModel filter, System.Collections.Generic.IReadOnlyList<ScrapReportItemViewModel> items)
            => Array.Empty<byte>();
    }

    private sealed class StubTransferPdfExporter : ITransferPeriodReportPdfExporter
    {
        public byte[] Export(TransferPeriodReportFilterViewModel filter, System.Collections.Generic.IReadOnlyList<DateTime> dates, System.Collections.Generic.IReadOnlyList<TransferPeriodReportItemViewModel> items)
            => Array.Empty<byte>();
    }

    private sealed class StubWipBatchPdfExporter : IWipBatchReportPdfExporter
    {
        public byte[] Export(WipBatchReportFilterViewModel filter, System.Collections.Generic.IReadOnlyList<WipBatchReportItemViewModel> items, decimal totalQuantity)
            => Array.Empty<byte>();
    }
}
