using System.IO;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Services;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class ImportServiceTests
{
    [Fact]
    public async Task ImportRoutesExcelAsync_CreatesWipBalanceWhenQuantityProvided()
    {
        await using var dbContext = CreateContext();
        var currentUser = new TestCurrentUserService();
        var service = new ImportService(dbContext, currentUser);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Маршруты");
        worksheet.Cell(1, 1).Value = "Наименование детали";
        worksheet.Cell(1, 2).Value = "Наименование операции";
        worksheet.Cell(1, 3).Value = "№ операции";
        worksheet.Cell(1, 4).Value = "Утвержденный норматив (н/ч)";
        worksheet.Cell(1, 5).Value = "Участок";
        worksheet.Cell(1, 6).Value = "Количество остатка";

        worksheet.Cell(2, 1).Value = "Деталь 1";
        worksheet.Cell(2, 2).Value = "Операция 1";
        worksheet.Cell(2, 3).Value = "10";
        worksheet.Cell(2, 4).Value = 1.5;
        worksheet.Cell(2, 5).Value = "Участок 1";
        worksheet.Cell(2, 6).Value = 12.3456;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var summary = await service.ImportRoutesExcelAsync(stream, "routes.xlsx");

        Assert.Equal(1, summary.TotalRows);
        Assert.Equal(1, summary.Succeeded);
        Assert.Equal(0, summary.Skipped);

        var balance = await dbContext.WipBalances.SingleAsync();
        Assert.Equal(12.346m, balance.Quantity);

        var route = await dbContext.PartRoutes.SingleAsync();
        Assert.Equal(route.PartId, balance.PartId);
        Assert.Equal(route.SectionId, balance.SectionId);
        Assert.Equal(route.OpNumber, balance.OpNumber);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    }
}
