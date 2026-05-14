using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Controllers;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;
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

        var actionResult = await controller.Index(partId, null, page: 2, pageSize: 2, cancellationToken: CancellationToken.None).ConfigureAwait(false);

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
        Assert.Equal(5, partGroup.Items.Count);
        Assert.Equal(2, partGroup.LabelGroups.Count);
        Assert.Equal(expectedSum, partGroup.TotalQuantity);
        Assert.Equal(5, partGroup.MovementCount);

        var itemDates = partGroup.Items.Select(x => x.AddedAt).ToArray();
        Assert.Contains(baseDate.AddDays(4), itemDates);
        Assert.Contains(baseDate.AddDays(3), itemDates);
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

        var actionResult = await controller.Index(partId, null, page: 10, pageSize: -5, cancellationToken: CancellationToken.None).ConfigureAwait(false);

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
        Assert.Equal(2, model.Areas.Count);
        Assert.DoesNotContain(model.Areas, x => x.Key == "wip");
        Assert.Contains(model.Areas, x => x.Key == "sgdu" && x.IsActive && x.IsEnabled);
        Assert.Contains(model.MovementTypes, x => x.Title == "Приход" && x.IsEnabled);
    }

    [Fact]
    public async Task Index_FiltersBySearchTextAndMovement()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var otherPartId = Guid.NewGuid();

        dbContext.Parts.AddRange(
            new Part { Id = partId, Name = "Корпус фильтра", Code = "KF-01" },
            new Part { Id = otherPartId, Name = "Пластина", Code = "PL-01" });

        var baseDate = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc);
        dbContext.WarehouseItems.AddRange(
            new WarehouseItem
            {
                Id = Guid.NewGuid(),
                PartId = partId,
                Quantity = 10m,
                MovementType = WarehouseMovementKind.Receipt,
                SourceType = WarehouseMovementKind.ManualReceipt,
                DocumentNumber = "DOC-KF-IN",
                AddedAt = baseDate,
                CreatedAt = baseDate,
            },
            new WarehouseItem
            {
                Id = Guid.NewGuid(),
                PartId = partId,
                Quantity = -4m,
                MovementType = WarehouseMovementKind.Issue,
                SourceType = WarehouseMovementKind.ManualIssue,
                DocumentNumber = "DOC-KF-OUT",
                AddedAt = baseDate.AddDays(1),
                CreatedAt = baseDate.AddDays(1),
            },
            new WarehouseItem
            {
                Id = Guid.NewGuid(),
                PartId = otherPartId,
                Quantity = 7m,
                MovementType = WarehouseMovementKind.Receipt,
                SourceType = WarehouseMovementKind.ManualReceipt,
                AddedAt = baseDate.AddDays(2),
                CreatedAt = baseDate.AddDays(2),
            });

        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        var controller = CreateController(dbContext);

        var actionResult = await controller.Index(
            null,
            "корпус",
            "issues",
            page: 1,
            pageSize: 20,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<WarehouseIndexViewModel>(viewResult.Model);

        Assert.Equal("issues", model.MovementFilter);
        Assert.Equal("корпус", model.PartSearch);
        Assert.Equal(1, model.Items.Count);
        Assert.Equal(WarehouseMovementKind.Issue, model.Items.Single().MovementType);
        Assert.Equal(2, model.TotalMovementCount);
        Assert.Equal(1, model.ReceiptMovementCount);
        Assert.Equal(1, model.IssueMovementCount);
        Assert.Equal(6m, model.TotalQuantity);
        Assert.Single(model.PartGroups);
        Assert.Equal(6m, model.PartGroups.Single().TotalQuantity);
    }

    [Fact]
    public async Task ManualReceipt_CreatesWarehouseItem()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        dbContext.Parts.Add(new Part { Id = partId, Name = "Готовая деталь", Code = "GD-01" });
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        var controller = CreateController(dbContext);

        var result = await controller.ManualReceipt(
            new WarehouseManualReceiptModel
            {
                PartId = partId,
                Quantity = 12.5m,
                ReceiptDate = new DateTime(2026, 5, 12),
                DocumentNumber = "УЧ-001",
                ControlCardNumber = "КК-001",
                ControllerName = "Контролер",
                MasterName = "Мастер",
                AcceptedByName = "Кладовщик",
                Comment = "Ручной тест",
                PrintControlCard = true,
            },
            CancellationToken.None).ConfigureAwait(false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(WarehouseController.Index), redirect.ActionName);

        var item = Assert.Single(dbContext.WarehouseItems);
        Assert.Equal(partId, item.PartId);
        Assert.Equal(12.5m, item.Quantity);
        Assert.Equal(WarehouseMovementKind.Receipt, item.MovementType);
        Assert.Equal(WarehouseMovementKind.ManualReceipt, item.SourceType);
        Assert.Equal("УЧ-001", item.DocumentNumber);
        Assert.Equal("КК-001", item.ControlCardNumber);
        Assert.Equal("Контролер", item.ControllerName);
        Assert.Equal("Мастер", item.MasterName);
        Assert.Equal("Кладовщик", item.AcceptedByName);
        Assert.Equal(item.Id.ToString(), controller.TempData["WarehousePrintItemId"]);
    }

    [Fact]
    public async Task ManualAssemblyUnitReceipt_CreatesLocalAssemblyUnitAndWarehouseItem()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.ManualAssemblyUnitReceipt(
            new WarehouseAssemblyUnitReceiptModel
            {
                AssemblyUnitName = "Узел СИП 01",
                Quantity = 4m,
                ReceiptDate = new DateTime(2026, 5, 13),
                DocumentNumber = "СУ-001",
                PrintControlCard = true,
            },
            CancellationToken.None).ConfigureAwait(false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(WarehouseController.Index), redirect.ActionName);

        var assemblyUnit = Assert.Single(dbContext.WarehouseAssemblyUnits);
        Assert.Equal("Узел СИП 01", assemblyUnit.Name);
        Assert.Equal("УЗЕЛ СИП 01", assemblyUnit.NormalizedName);

        var item = Assert.Single(dbContext.WarehouseItems);
        Assert.Null(item.PartId);
        Assert.Equal(assemblyUnit.Id, item.AssemblyUnitId);
        Assert.Equal(4m, item.Quantity);
        Assert.Equal(WarehouseMovementKind.Receipt, item.MovementType);
        Assert.Equal(WarehouseMovementKind.ManualReceipt, item.SourceType);
        Assert.Equal(item.Id.ToString(), controller.TempData["WarehousePrintItemId"]);
    }

    [Fact]
    public async Task ManualAssemblyUnitReceipt_ReusesExistingLocalAssemblyUnitByName()
    {
        await using var dbContext = CreateContext();
        var existing = new WarehouseAssemblyUnit
        {
            Id = Guid.NewGuid(),
            Name = "Узел СИП 02",
            NormalizedName = "УЗЕЛ СИП 02",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        dbContext.WarehouseAssemblyUnits.Add(existing);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        var controller = CreateController(dbContext);

        await controller.ManualAssemblyUnitReceipt(
            new WarehouseAssemblyUnitReceiptModel
            {
                AssemblyUnitName = "  Узел   СИП 02  ",
                Quantity = 2m,
                ReceiptDate = new DateTime(2026, 5, 13),
            },
            CancellationToken.None).ConfigureAwait(false);

        var assemblyUnit = Assert.Single(dbContext.WarehouseAssemblyUnits);
        Assert.Equal(existing.Id, assemblyUnit.Id);

        var item = Assert.Single(dbContext.WarehouseItems);
        Assert.Equal(existing.Id, item.AssemblyUnitId);
        Assert.Equal(2m, item.Quantity);
    }

    [Fact]
    public async Task ManualIssue_CreatesNegativeWarehouseMovement()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        dbContext.Parts.Add(new Part { Id = partId, Name = "Деталь под СИП" });
        dbContext.WarehouseItems.Add(new WarehouseItem
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            Quantity = 10m,
            MovementType = WarehouseMovementKind.Receipt,
            SourceType = WarehouseMovementKind.ManualReceipt,
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        var controller = CreateController(dbContext);

        var result = await controller.ManualIssue(
            new WarehouseManualIssueModel
            {
                PartId = partId,
                Quantity = 3.5m,
                IssueDate = new DateTime(2026, 5, 13),
                DocumentNumber = "РС-001",
            },
            CancellationToken.None).ConfigureAwait(false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(WarehouseController.Index), redirect.ActionName);

        var issue = Assert.Single(dbContext.WarehouseItems, x => x.MovementType == WarehouseMovementKind.Issue);
        Assert.Equal(partId, issue.PartId);
        Assert.Equal(-3.5m, issue.Quantity);
        Assert.Equal(WarehouseMovementKind.ManualIssue, issue.SourceType);
        Assert.Equal("Сборщик СИП отдел", issue.AcceptedByName);
        Assert.Equal(6.5m, dbContext.WarehouseItems.Sum(x => x.Quantity));
    }

    [Fact]
    public async Task ManualAssemblyUnitIssue_CreatesNegativeWarehouseMovement()
    {
        await using var dbContext = CreateContext();
        var assemblyUnit = new WarehouseAssemblyUnit
        {
            Id = Guid.NewGuid(),
            Name = "Узел СИП 03",
            NormalizedName = "УЗЕЛ СИП 03",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        dbContext.WarehouseAssemblyUnits.Add(assemblyUnit);
        dbContext.WarehouseItems.Add(new WarehouseItem
        {
            Id = Guid.NewGuid(),
            AssemblyUnitId = assemblyUnit.Id,
            Quantity = 8m,
            MovementType = WarehouseMovementKind.Receipt,
            SourceType = WarehouseMovementKind.ManualReceipt,
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        var controller = CreateController(dbContext);

        var result = await controller.ManualAssemblyUnitIssue(
            new WarehouseAssemblyUnitIssueModel
            {
                AssemblyUnitId = assemblyUnit.Id,
                AssemblyUnitName = assemblyUnit.Name,
                Quantity = 5m,
                IssueDate = new DateTime(2026, 5, 13),
            },
            CancellationToken.None).ConfigureAwait(false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(WarehouseController.Index), redirect.ActionName);

        var issue = Assert.Single(dbContext.WarehouseItems, x => x.MovementType == WarehouseMovementKind.Issue);
        Assert.Null(issue.PartId);
        Assert.Equal(assemblyUnit.Id, issue.AssemblyUnitId);
        Assert.Equal(-5m, issue.Quantity);
        Assert.Equal(WarehouseMovementKind.ManualIssue, issue.SourceType);
        Assert.Equal(3m, dbContext.WarehouseItems.Sum(x => x.Quantity));
    }

    [Fact]
    public async Task ManualIssue_DoesNotCreateMovement_WhenQuantityExceedsBalance()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        dbContext.Parts.Add(new Part { Id = partId, Name = "Ограниченная деталь" });
        dbContext.WarehouseItems.Add(new WarehouseItem
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            Quantity = 2m,
            MovementType = WarehouseMovementKind.Receipt,
            SourceType = WarehouseMovementKind.ManualReceipt,
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        var controller = CreateController(dbContext);

        await controller.ManualIssue(
            new WarehouseManualIssueModel
            {
                PartId = partId,
                Quantity = 5m,
                IssueDate = new DateTime(2026, 5, 13),
            },
            CancellationToken.None).ConfigureAwait(false);

        Assert.Single(dbContext.WarehouseItems);
        Assert.Contains("Недостаточно остатка", Assert.IsType<string>(controller.TempData["WarehouseError"]));
    }

    private static WarehouseController CreateController(AppDbContext dbContext)
    {
        var controller = new WarehouseController(
            dbContext,
            new TestCurrentUserService(),
            new TestWarehouseControlCardDocumentService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };

        controller.TempData = new TempDataDictionary(controller.HttpContext!, new TestTempDataProvider());

        return controller;
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId => Guid.Empty;
    }

    private sealed class TestWarehouseControlCardDocumentService : IWarehouseControlCardDocumentService
    {
        public Task<WarehouseControlCardDocumentResult> BuildAsync(Guid warehouseItemId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WarehouseControlCardDocumentResult(
                "card.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                Array.Empty<byte>()));
        }
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
