using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Controllers;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class HomeControllerTests
{
    [Fact]
    public async Task GetBubbleLabels_ReturnsModeratedDistinctPool()
    {
        await using var dbContext = CreateContext();

        var longName = new string('А', 130);
        var displayTrimCandidate = $"{new string('Б', 50)} (X-1)";

        dbContext.Parts.AddRange(new List<Part>
        {
            new() { Id = Guid.NewGuid(), Name = "Корпус", Code = "A-01" },
            new() { Id = Guid.NewGuid(), Name = "Корпус", Code = "A-01" },
            new() { Id = Guid.NewGuid(), Name = " ", Code = null },
            new() { Id = Guid.NewGuid(), Name = "null", Code = null },
            new() { Id = Guid.NewGuid(), Name = longName, Code = "A-999" },
            new() { Id = Guid.NewGuid(), Name = displayTrimCandidate, Code = null },
            new() { Id = Guid.NewGuid(), Name = "Скоба", Code = "B-02" },
        });

        await dbContext.SaveChangesAsync();

        var controller = new HomeController(NullLogger<HomeController>.Instance, dbContext);

        var actionResult = await controller.GetBubbleLabels(20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);

        var items = ExtractItems(okResult.Value);
        Assert.NotNull(items);
        Assert.Contains("Корпус (A-01)", items);
        Assert.Contains("Скоба (B-02)", items);
        Assert.DoesNotContain("null", items, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(items, x => x.Length > 42);
        Assert.Equal(items.Distinct(StringComparer.OrdinalIgnoreCase).Count(), items.Count);
    }

    [Fact]
    public async Task GetBubbleLabels_ClampsRequestedPoolSize()
    {
        await using var dbContext = CreateContext();

        for (var index = 0; index < 130; index += 1)
        {
            dbContext.Parts.Add(new Part
            {
                Id = Guid.NewGuid(),
                Name = $"Деталь {index:D3}",
                Code = $"C-{index:D3}",
            });
        }

        await dbContext.SaveChangesAsync();

        var controller = new HomeController(NullLogger<HomeController>.Instance, dbContext);

        var minLimitResult = await controller.GetBubbleLabels(1, CancellationToken.None);
        var minItems = ExtractItems(Assert.IsType<OkObjectResult>(minLimitResult).Value);
        Assert.Equal(20, minItems.Count);

        var maxLimitResult = await controller.GetBubbleLabels(150, CancellationToken.None);
        var maxItems = ExtractItems(Assert.IsType<OkObjectResult>(maxLimitResult).Value);
        Assert.Equal(100, maxItems.Count);
    }

    private static List<string> ExtractItems(object? value)
    {
        var itemsProperty = value?.GetType().GetProperty("Items");
        var itemsValue = itemsProperty?.GetValue(value);

        return itemsValue as List<string> ?? ((itemsValue as IEnumerable<string>)?.ToList() ?? new List<string>());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}
