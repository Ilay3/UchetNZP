using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Controllers;
using UchetNZP.Web.Models;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class WarehouseControllerTests
{
    [Fact]
    public async Task Index_ReturnsPagedItemsAndGroups()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var part = new Part
        {
            Id = partId,
            Name = "Корпус",
            Code = "A-01",
        };

        var labelOne = new WipLabel
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            LabelDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Quantity = 10m,
            RemainingQuantity = 5m,
            Number = "L-001",
        };

        var labelTwo = new WipLabel
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            LabelDate = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Quantity = 12m,
            RemainingQuantity = 6m,
            Number = "L-002",
        };

        dbContext.Parts.Add(part);
        dbContext.WipLabels.AddRange(labelOne, labelTwo);

        var baseDate = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < 5; index++)
        {
            var label = index % 2 == 0 ? labelOne : labelTwo;

            var item = new WarehouseItem
            {
                Id = Guid.NewGuid(),
                PartId = partId,
                Quantity = 5m + index,
                AddedAt = baseDate.AddDays(index),
                CreatedAt = baseDate.AddDays(index),
                UpdatedAt = baseDate.AddDays(index).AddHours(2),
                Comment = $"Запись {index}",
            };

            item.WarehouseLabelItems.Add(new WarehouseLabelItem
            {
                Id = Guid.NewGuid(),
                WarehouseItemId = item.Id,
                WipLabelId = label.Id,
                Quantity = 1m + index,
                AddedAt = baseDate.AddDays(index),
                UpdatedAt = baseDate.AddDays(index).AddHours(1),
            });

            dbContext.WarehouseItems.Add(item);
        }

        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        var controller = CreateController(dbContext);

        var actionResult = await controller.Index(partId, 2, 2, CancellationToken.None).ConfigureAwait(false);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<WarehouseIndexViewModel>(viewResult.Model);

        Assert.Equal(partId, model.SelectedPartId);
        Assert.Equal(2, model.CurrentPage);
        Assert.Equal(2, model.PageSize);
        Assert.Equal(3, model.TotalPages);
        Assert.Equal(2, model.Items.Count);

        var expectedSum = Enumerable.Range(0, 5).Sum(x => 5m + x);
        Assert.Equal(expectedSum, model.TotalQuantity);

        var partGroup = Assert.Single(model.PartGroups);
        Assert.Equal(2, partGroup.Items.Count);
        Assert.Equal(2, partGroup.LabelGroups.Count);

        var itemDates = partGroup.Items.Select(x => x.AddedAt).ToArray();
        Assert.DoesNotContain(baseDate.AddDays(4), itemDates);
        Assert.DoesNotContain(baseDate.AddDays(3), itemDates);
        Assert.Contains(baseDate.AddDays(2), itemDates);
        Assert.Contains(baseDate.AddDays(1), itemDates);
    }

    [Fact]
    public async Task Index_NormalizesOutOfRangeParameters()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var part = new Part { Id = partId, Name = "Пластина" };
        dbContext.Parts.Add(part);

        var baseDate = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 3; index++)
        {
            dbContext.WarehouseItems.Add(new WarehouseItem
            {
                Id = Guid.NewGuid(),
                PartId = partId,
                Quantity = 3m,
                AddedAt = baseDate.AddDays(index),
                CreatedAt = baseDate.AddDays(index),
            });
        }

        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        var controller = CreateController(dbContext);

        var actionResult = await controller.Index(partId, 10, -5, CancellationToken.None).ConfigureAwait(false);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<WarehouseIndexViewModel>(viewResult.Model);

        Assert.Equal(1, model.CurrentPage);
        Assert.Equal(20, model.PageSize);
        Assert.Equal(1, model.TotalPages);
        Assert.Equal(3, model.Items.Count);
    }

    [Fact]
    public async Task Index_ReturnsDefaultModel_WhenNoData()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);

        var actionResult = await controller.Index(null, cancellationToken: CancellationToken.None).ConfigureAwait(false);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<WarehouseIndexViewModel>(viewResult.Model);

        Assert.Null(model.SelectedPartId);
        Assert.Equal(0, model.TotalQuantity);
        Assert.Empty(model.Items);
        Assert.Empty(model.PartGroups);
        Assert.Equal(1, model.CurrentPage);
        Assert.Equal(20, model.PageSize);
        Assert.Equal(0, model.TotalPages);
        Assert.Single(model.Parts);
    }

    private static WarehouseController CreateController(AppDbContext dbContext)
    {
        var controller = new WarehouseController(dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };

        controller.TempData = new TempDataDictionary(controller.HttpContext!, new TestTempDataProvider());

        return controller;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object?>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
