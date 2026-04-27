using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Imports;
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
        worksheet.Cell(1, 2).Value = "Код детали";
        worksheet.Cell(1, 3).Value = "№ операции";
        worksheet.Cell(1, 4).Value = "Наименование операции";
        worksheet.Cell(1, 5).Value = "Вид работ";
        worksheet.Cell(1, 6).Value = "Норматив, н/ч";
        worksheet.Cell(1, 7).Value = "Количество остатка";

        worksheet.Cell(2, 1).Value = "Деталь 1";
        worksheet.Cell(2, 2).Value = "КОД-1";
        worksheet.Cell(2, 3).Value = "10";
        worksheet.Cell(2, 4).Value = "Операция 1";
        worksheet.Cell(2, 5).Value = "Вид работ 1";
        worksheet.Cell(2, 6).Value = 1.5;
        worksheet.Cell(2, 7).Value = 12.3456;

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

        var part = await dbContext.Parts.SingleAsync();
        Assert.Equal("Деталь 1", part.Name);
        Assert.Equal("КОД-1", part.Code);
    }

    [Fact]
    public async Task ImportRoutesExcelAsync_ReimportUpdatesExistingRouteAndAddsNewOnes()
    {
        await using var dbContext = CreateContext();
        var currentUser = new TestCurrentUserService();
        var service = new ImportService(dbContext, currentUser);

        await ImportWorkbookAsync(service, worksheet =>
        {
            worksheet.Cell(1, 1).Value = "Наименование детали";
            worksheet.Cell(1, 2).Value = "Код детали";
            worksheet.Cell(1, 3).Value = "№ операции";
            worksheet.Cell(1, 4).Value = "Наименование операции";
            worksheet.Cell(1, 5).Value = "Вид работ";
            worksheet.Cell(1, 6).Value = "Норматив, н/ч";

            worksheet.Cell(2, 1).Value = "Боковина";
            worksheet.Cell(2, 2).Value = "ТЭМ2.70.900.355";
            worksheet.Cell(2, 3).Value = "005";
            worksheet.Cell(2, 4).Value = "Заготовительная";
            worksheet.Cell(2, 5).Value = "Заготовительная";
            worksheet.Cell(2, 6).Value = 0.017;
        });

        var secondSummary = await ImportWorkbookAsync(service, worksheet =>
        {
            worksheet.Cell(1, 1).Value = "Наименование детали";
            worksheet.Cell(1, 2).Value = "Код детали";
            worksheet.Cell(1, 3).Value = "№ операции";
            worksheet.Cell(1, 4).Value = "Наименование операции";
            worksheet.Cell(1, 5).Value = "Вид работ";
            worksheet.Cell(1, 6).Value = "Норматив, н/ч";

            worksheet.Cell(2, 1).Value = "Боковина";
            worksheet.Cell(2, 2).Value = "ТЭМ2.70.900.355";
            worksheet.Cell(2, 3).Value = "005";
            worksheet.Cell(2, 4).Value = "Слесарная";
            worksheet.Cell(2, 5).Value = "Слесарная";
            worksheet.Cell(2, 6).Value = 0.056;

            worksheet.Cell(3, 1).Value = "Боковина";
            worksheet.Cell(3, 2).Value = "ТЭМ2.70.900.355";
            worksheet.Cell(3, 3).Value = "010";
            worksheet.Cell(3, 4).Value = "Фрезерная";
            worksheet.Cell(3, 5).Value = "Фрезерная";
            worksheet.Cell(3, 6).Value = 0.1;
        });

        Assert.Equal(2, secondSummary.TotalRows);
        Assert.Equal(2, secondSummary.Succeeded);
        Assert.Equal(0, secondSummary.Skipped);

        var routes = await dbContext.PartRoutes
            .Include(x => x.Operation)
            .Include(x => x.Section)
            .OrderBy(x => x.OpNumber)
            .ToListAsync();

        Assert.Equal(2, routes.Count);

        var part = await dbContext.Parts.SingleAsync();
        Assert.Equal("Боковина", part.Name);
        Assert.Equal("ТЭМ2.70.900.355", part.Code);

        Assert.NotNull(routes[0].Operation);
        Assert.NotNull(routes[0].Section);
        Assert.NotNull(routes[1].Operation);
        Assert.NotNull(routes[1].Section);
        Assert.Equal("Слесарная", routes[0].Operation!.Name);
        Assert.Equal("Слесарная", routes[0].Section!.Name);
        Assert.Equal(0.056m, routes[0].NormHours);
        Assert.Equal("Фрезерная", routes[1].Operation!.Name);
        Assert.Equal("Фрезерная", routes[1].Section!.Name);
        Assert.Equal(0.1m, routes[1].NormHours);
    }

    [Fact]
    public async Task ImportMetalDataExcelAsync_UsesColumnsBtoFAndDefaultsCoefficientAndDisplayName()
    {
        await using var dbContext = CreateContext();
        var service = new ImportService(dbContext, new TestCurrentUserService());

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Материалы и коэф. металлов");
        sheet.Cell(1, 2).Value = "Артикул";
        sheet.Cell(1, 3).Value = "Наименование";
        sheet.Cell(1, 4).Value = "Вес";
        sheet.Cell(1, 5).Value = "Коэф";
        sheet.Cell(1, 6).Value = "Выбор";

        sheet.Cell(2, 2).Value = " MAT-01 ";
        sheet.Cell(2, 3).Value = "Лист 09Г2С";
        sheet.Cell(2, 4).Value = 12.5;
        sheet.Cell(2, 5).Value = string.Empty;
        sheet.Cell(2, 6).Value = string.Empty;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var summary = await service.ImportMetalDataExcelAsync(stream, "materials.xlsx", MetalImportMode.Materials, false);

        Assert.Equal(1, summary.MaterialsImported);
        Assert.Equal(1, summary.MaterialsCreated);
        Assert.Equal(0, summary.MaterialsUpdated);
        Assert.Equal(0, summary.MaterialsSkipped);

        var material = await dbContext.MetalMaterials.SingleAsync();
        Assert.Equal("MAT-01", material.Code);
        Assert.Equal("Лист 09Г2С", material.Name);
        Assert.Equal("MAT-01 | Лист 09Г2С", material.DisplayName);
        Assert.Equal(1m, material.Coefficient);
        Assert.Equal(1m, material.CoefConsumption);
        Assert.Equal(12.5m, material.WeightPerUnitKg);
        Assert.Equal(12.5m, material.MassPerSquareMeterKg);
        Assert.Equal(0m, material.MassPerMeterKg);
        Assert.Equal("SquareMeter", material.UnitKind);
        Assert.Equal("m2", material.StockUnit);
        Assert.Contains(summary.Warnings, x => x.Message.Contains("Коэффициент не заполнен", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportMetalDataExcelAsync_ReimportByNormalizedCodeAndNameDoesNotCreateDuplicates()
    {
        await using var dbContext = CreateContext();
        var service = new ImportService(dbContext, new TestCurrentUserService());

        await ImportMaterialsWorkbookAsync(service, sheet =>
        {
            sheet.Cell(1, 2).Value = "Артикул";
            sheet.Cell(1, 3).Value = "Наименование";
            sheet.Cell(1, 4).Value = "Вес";
            sheet.Cell(1, 5).Value = "Коэф";
            sheet.Cell(1, 6).Value = "Выбор";

            sheet.Cell(2, 2).Value = "ab-01";
            sheet.Cell(2, 3).Value = "Круг 45";
            sheet.Cell(2, 4).Value = 2;
            sheet.Cell(2, 5).Value = 1.2;

            sheet.Cell(3, 2).Value = string.Empty;
            sheet.Cell(3, 3).Value = "  Лента 10  ";
            sheet.Cell(3, 4).Value = 3;
            sheet.Cell(3, 5).Value = 1;
        });

        var reimportSummary = await ImportMaterialsWorkbookAsync(service, sheet =>
        {
            sheet.Cell(1, 2).Value = "Артикул";
            sheet.Cell(1, 3).Value = "Наименование";
            sheet.Cell(1, 4).Value = "Вес";
            sheet.Cell(1, 5).Value = "Коэф";
            sheet.Cell(1, 6).Value = "Выбор";

            sheet.Cell(2, 2).Value = " AB 01 ";
            sheet.Cell(2, 3).Value = "Круг 45";
            sheet.Cell(2, 4).Value = 2.5;
            sheet.Cell(2, 5).Value = 1.3;

            sheet.Cell(3, 2).Value = string.Empty;
            sheet.Cell(3, 3).Value = "Лента10";
            sheet.Cell(3, 4).Value = 3.5;
            sheet.Cell(3, 5).Value = 1.4;
        });

        Assert.Equal(0, reimportSummary.MaterialsCreated);
        Assert.Equal(2, reimportSummary.MaterialsUpdated);

        var materials = await dbContext.MetalMaterials.OrderBy(x => x.Name).ToListAsync();
        Assert.Equal(2, materials.Count);
        Assert.Contains(materials, x => x.Code == "ab-01" && x.Coefficient == 1.3m && x.MassPerMeterKg == 2.5m);
        Assert.Contains(materials, x => x.Code == null && x.Coefficient == 1.4m && x.MassPerSquareMeterKg == 3.5m);
    }

    [Fact]
    public async Task ImportMetalDataExcelAsync_UnresolvedUnitTypeReturnsWarningAndSkipsRow()
    {
        await using var dbContext = CreateContext();
        var service = new ImportService(dbContext, new TestCurrentUserService());

        var summary = await ImportMaterialsWorkbookAsync(service, sheet =>
        {
            sheet.Cell(1, 2).Value = "Артикул";
            sheet.Cell(1, 3).Value = "Наименование";
            sheet.Cell(1, 4).Value = "Вес";
            sheet.Cell(1, 5).Value = "Коэф";
            sheet.Cell(1, 6).Value = "Выбор";

            sheet.Cell(2, 2).Value = "X-1";
            sheet.Cell(2, 3).Value = "Материал без типа";
            sheet.Cell(2, 4).Value = 1;
            sheet.Cell(2, 5).Value = 1;
        });

        Assert.Equal(0, summary.MaterialsImported);
        Assert.Equal(1, summary.MaterialsSkipped);
        Assert.Contains(summary.Warnings, x => x.Message.Contains("тип единицы", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary.Errors, x => x.Message.Contains("UnitKind/StockUnit", StringComparison.Ordinal));
        Assert.Empty(dbContext.MetalMaterials);
        Assert.Equal("skipped", summary.MaterialPreviewRows.Single().Status);
        Assert.True(summary.MaterialPreviewRows.Single().UnresolvedUnitType);
    }

    private static async Task<ImportSummaryDto> ImportWorkbookAsync(ImportService service, Action<IXLWorksheet> configure)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Маршруты");
        configure(worksheet);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return await service.ImportRoutesExcelAsync(stream, "routes.xlsx");
    }

    private static async Task<MetalDataImportSummaryDto> ImportMaterialsWorkbookAsync(ImportService service, Action<IXLWorksheet> configure)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Материалы и коэф. металлов");
        configure(worksheet);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return await service.ImportMetalDataExcelAsync(stream, "materials.xlsx", MetalImportMode.Materials, false);
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
