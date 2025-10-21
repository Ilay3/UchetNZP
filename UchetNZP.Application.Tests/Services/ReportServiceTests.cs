using System.Linq;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class ReportServiceTests
{
    [Fact]
    public async Task ExportRoutesToExcelAsync_FiltersDataAndBuildsWorkbook()
    {
        // Arrange
        await using var dbContext = CreateContext();
        var sectionA = new Section { Id = Guid.NewGuid(), Name = "Сборка", Code = "S1" };
        var sectionB = new Section { Id = Guid.NewGuid(), Name = "Окраска" };
        var partA = new Part { Id = Guid.NewGuid(), Name = "Деталь А", Code = "DA-01" };
        var partB = new Part { Id = Guid.NewGuid(), Name = "Деталь B" };
        var operationCut = new Operation { Id = Guid.NewGuid(), Name = "Резка" };
        var operationPaint = new Operation { Id = Guid.NewGuid(), Name = "Покраска" };

        dbContext.Sections.AddRange(sectionA, sectionB);
        dbContext.Parts.AddRange(partA, partB);
        dbContext.Operations.AddRange(operationCut, operationPaint);
        dbContext.PartRoutes.AddRange(
            new PartRoute
            {
                Id = Guid.NewGuid(),
                PartId = partA.Id,
                OperationId = operationCut.Id,
                SectionId = sectionA.Id,
                OpNumber = 10,
                NormHours = 1.5m,
            },
            new PartRoute
            {
                Id = Guid.NewGuid(),
                PartId = partB.Id,
                OperationId = operationPaint.Id,
                SectionId = sectionB.Id,
                OpNumber = 20,
                NormHours = 0.75m,
            });
        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partA.Id,
            SectionId = sectionA.Id,
            OpNumber = 10,
            Quantity = 12.5m,
        });
        await dbContext.SaveChangesAsync();

        var service = new ReportService(dbContext);

        // Act
        var result = await service.ExportRoutesToExcelAsync(partA.Name, sectionA.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        using var stream = new MemoryStream(result);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Маршруты");

        var headerRow = worksheet
            .RowsUsed()
            .First(row => string.Equals(row.Cell(1).GetString(), "Деталь", StringComparison.OrdinalIgnoreCase));

        var dataRows = worksheet
            .RowsUsed()
            .Where(row => row.RowNumber() > headerRow.RowNumber())
            .ToArray();

        Assert.Single(dataRows);
        var dataRow = dataRows.Single();
        Assert.Equal(partA.Name, dataRow.Cell(1).GetString());
        Assert.Equal(partA.Code, dataRow.Cell(2).GetString());
        Assert.Equal("010", dataRow.Cell(3).GetString());
        Assert.Equal(operationCut.Name, dataRow.Cell(4).GetString());
        Assert.Equal(sectionA.Name, dataRow.Cell(5).GetString());
        Assert.Equal(1.5m, dataRow.Cell(6).GetValue<decimal>());
        Assert.Equal(12.5m, dataRow.Cell(7).GetValue<decimal>());

        var sectionFilterCell = worksheet
            .CellsUsed()
            .FirstOrDefault(cell => cell.GetString() == "Участок");

        Assert.NotNull(sectionFilterCell);
        var sectionValue = worksheet.Cell(sectionFilterCell!.Address.RowNumber, sectionFilterCell.Address.ColumnNumber + 1).GetString();
        Assert.Equal("Сборка (S1)", sectionValue);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
