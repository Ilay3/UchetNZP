using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Transfers;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class TransferServiceTests
{
    [Fact]
    public async Task AddTransfersBatchAsync_WithoutScrap_UpdatesBalances()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var toSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var toOperation = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, toSection);
        dbContext.Operations.AddRange(fromOperation, toOperation);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10),
            CreateRoute(part.Id, toSection.Id, toOperation.Id, 20));

        var fromBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 100m,
        };

        var toBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = toSection.Id,
            OpNumber = 20,
            Quantity = 15m,
        };

        dbContext.WipBalances.AddRange(fromBalance, toBalance);

        await dbContext.SaveChangesAsync();

        var routeService = new RouteService(dbContext);
        var currentUser = new TestCurrentUserService();
        var service = new TransferService(dbContext, routeService, currentUser);

        var transferDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 40m, "", null, null)
        });

        Assert.Equal(1, summary.Saved);
        var item = Assert.Single(summary.Items);
        Assert.Equal(60m, item.FromBalanceAfter);
        Assert.Equal(55m, item.ToBalanceAfter);
        Assert.Null(item.Scrap);

        var updatedFromBalance = await dbContext.WipBalances.SingleAsync(x => x.Id == fromBalance.Id);
        Assert.Equal(60m, updatedFromBalance.Quantity);

        var updatedToBalance = await dbContext.WipBalances.SingleAsync(x => x.Id == toBalance.Id);
        Assert.Equal(55m, updatedToBalance.Quantity);

        Assert.Empty(dbContext.WipScraps);

        var storedTransfer = await dbContext.WipTransfers.SingleAsync();
        Assert.Null(storedTransfer.WipLabelId);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithScrap_ZeroesFromBalanceAndCreatesScrap()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var toSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var toOperation = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, toSection);
        dbContext.Operations.AddRange(fromOperation, toOperation);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10),
            CreateRoute(part.Id, toSection.Id, toOperation.Id, 20));

        var fromBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 120m,
        };

        dbContext.WipBalances.Add(fromBalance);

        await dbContext.SaveChangesAsync();

        var routeService = new RouteService(dbContext);
        var currentUser = new TestCurrentUserService();
        var service = new TransferService(dbContext, routeService, currentUser);

        var transferDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var comment = "Сломалось";

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(
                part.Id,
                10,
                20,
                transferDate,
                80m,
                null,
                null,
                new TransferScrapDto(ScrapType.Technological, 40m, comment))
        });

        Assert.Equal(1, summary.Saved);
        var item = Assert.Single(summary.Items);
        Assert.Equal(0m, item.FromBalanceAfter);
        Assert.NotNull(item.Scrap);
        Assert.Equal(40m, item.Scrap!.Quantity);
        Assert.Equal(ScrapType.Technological, item.Scrap.ScrapType);
        Assert.Equal(comment, item.Scrap.Comment);

        var updatedFromBalance = await dbContext.WipBalances.SingleAsync(x => x.Id == fromBalance.Id);
        Assert.Equal(0m, updatedFromBalance.Quantity);

        var scrap = await dbContext.WipScraps.Include(x => x.Transfer).SingleAsync();
        Assert.Equal(40m, scrap.Quantity);
        Assert.Equal(ScrapType.Technological, scrap.ScrapType);
        Assert.Equal(currentUser.UserId, scrap.UserId);
        Assert.Equal(comment, scrap.Comment);
        Assert.Equal(10, scrap.OpNumber);
        Assert.Equal(fromSection.Id, scrap.SectionId);
        Assert.Equal(part.Id, scrap.PartId);
        Assert.True(scrap.RecordedAt > DateTime.MinValue);
        Assert.NotNull(scrap.Transfer);
        Assert.Equal(item.TransferId, scrap.Transfer!.Id);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_ToWarehouse_StoresWarehouseEntry()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, new Section { Id = WarehouseDefaults.SectionId, Name = WarehouseDefaults.SectionName });
        dbContext.Operations.AddRange(fromOperation, new Operation { Id = WarehouseDefaults.OperationId, Name = WarehouseDefaults.OperationName });
        dbContext.PartRoutes.Add(CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10));

        var fromBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 120m,
        };

        dbContext.WipBalances.Add(fromBalance);

        var existingWarehouseItem = new WarehouseItem
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            Quantity = 5m,
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        dbContext.WarehouseItems.Add(existingWarehouseItem);

        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService());

        var transferDate = new DateTime(2025, 2, 2, 0, 0, 0, DateTimeKind.Utc);
        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, WarehouseDefaults.OperationNumber, transferDate, 40m, "Склад", null, null),
        });

        var item = Assert.Single(summary.Items);
        Assert.Equal(WarehouseDefaults.SectionId, item.ToSectionId);
        Assert.Equal(5m, item.ToBalanceBefore);
        Assert.Equal(45m, item.ToBalanceAfter);

        var updatedFromBalance = await dbContext.WipBalances.SingleAsync(x => x.Id == fromBalance.Id);
        Assert.Equal(80m, updatedFromBalance.Quantity);

        Assert.Equal(2, await dbContext.WarehouseItems.CountAsync());
        var newEntry = await dbContext.WarehouseItems
            .Where(x => x.TransferId == item.TransferId)
            .SingleAsync();

        Assert.Equal(40m, newEntry.Quantity);
        Assert.Equal(transferDate, newEntry.AddedAt);
        Assert.Equal(part.Id, newEntry.PartId);
        Assert.Equal("Склад", newEntry.Comment);
        Assert.Null(await dbContext.WipBalances.FirstOrDefaultAsync(x => x.OpNumber == WarehouseDefaults.OperationNumber));
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithLabel_TransferBetweenOperations_DoesNotReduceRemainingQuantity()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var toSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var toOperation = new Operation { Id = Guid.NewGuid(), Name = "020" };
        var labelId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, toSection);
        dbContext.Operations.AddRange(fromOperation, toOperation);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10),
            CreateRoute(part.Id, toSection.Id, toOperation.Id, 20));

        var fromBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 100m,
        };

        var toBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = toSection.Id,
            OpNumber = 20,
            Quantity = 0m,
        };

        dbContext.WipBalances.AddRange(fromBalance, toBalance);

        var label = new WipLabel
        {
            Id = labelId,
            PartId = part.Id,
            LabelDate = DateTime.SpecifyKind(new DateTime(2025, 1, 1), DateTimeKind.Unspecified),
            Quantity = 100m,
            RemainingQuantity = 100m,
            Number = "00001",
            IsAssigned = true,
        };

        var receipt = new WipReceipt
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            ReceiptDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 100m,
            UserId = Guid.NewGuid(),
            WipLabelId = labelId,
        };

        label.WipReceipt = receipt;

        dbContext.WipLabels.Add(label);
        dbContext.WipReceipts.Add(receipt);

        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService());
        var transferDate = new DateTime(2025, 3, 3, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 40m, null, labelId, null),
        });

        Assert.Equal(1, summary.Saved);
        var storedLabel = await dbContext.WipLabels.SingleAsync(x => x.Id == labelId);
        Assert.Equal(100m, storedLabel.RemainingQuantity);

        var transfer = await dbContext.WipTransfers.SingleAsync();
        Assert.Equal(labelId, transfer.WipLabelId);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WarehouseTransferWithLabel_CreatesWarehouseLabelItem()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var labelId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(
            fromSection,
            new Section { Id = WarehouseDefaults.SectionId, Name = WarehouseDefaults.SectionName });
        dbContext.Operations.AddRange(
            fromOperation,
            new Operation { Id = WarehouseDefaults.OperationId, Name = WarehouseDefaults.OperationName });
        dbContext.PartRoutes.Add(CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10));

        var fromBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 80m,
        };

        dbContext.WipBalances.Add(fromBalance);

        var label = new WipLabel
        {
            Id = labelId,
            PartId = part.Id,
            LabelDate = DateTime.SpecifyKind(new DateTime(2025, 1, 15), DateTimeKind.Unspecified),
            Quantity = 80m,
            RemainingQuantity = 80m,
            Number = "01001",
            IsAssigned = true,
        };

        var receipt = new WipReceipt
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            ReceiptDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 80m,
            UserId = Guid.NewGuid(),
            WipLabelId = labelId,
        };

        label.WipReceipt = receipt;

        dbContext.WipLabels.Add(label);
        dbContext.WipReceipts.Add(receipt);

        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService());
        var transferDate = new DateTime(2025, 5, 5, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, WarehouseDefaults.OperationNumber, transferDate, 30m, null, labelId, null),
        });

        var item = Assert.Single(summary.Items);

        var warehouseItem = await dbContext.WarehouseItems
            .SingleAsync(x => x.TransferId == item.TransferId);

        var warehouseLabelItem = await dbContext.WarehouseLabelItems
            .SingleAsync(x => x.WarehouseItemId == warehouseItem.Id);

        Assert.Equal(labelId, warehouseLabelItem.WipLabelId);
        Assert.Equal(30m, warehouseLabelItem.Quantity);
        Assert.Equal(transferDate, warehouseLabelItem.AddedAt);
        Assert.NotNull(warehouseLabelItem.UpdatedAt);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithLabelMovedToNextOperation_AllowsFurtherTransfer()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var section30 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 30" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };
        var operation30 = new Operation { Id = Guid.NewGuid(), Name = "030" };
        var labelId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20, section30);
        dbContext.Operations.AddRange(operation10, operation20, operation30);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20),
            CreateRoute(part.Id, section30.Id, operation30.Id, 30));

        dbContext.WipBalances.AddRange(
            new WipBalance
            {
                Id = Guid.NewGuid(),
                PartId = part.Id,
                SectionId = section10.Id,
                OpNumber = 10,
                Quantity = 100m,
            },
            new WipBalance
            {
                Id = Guid.NewGuid(),
                PartId = part.Id,
                SectionId = section20.Id,
                OpNumber = 20,
                Quantity = 0m,
            },
            new WipBalance
            {
                Id = Guid.NewGuid(),
                PartId = part.Id,
                SectionId = section30.Id,
                OpNumber = 30,
                Quantity = 0m,
            });

        var label = new WipLabel
        {
            Id = labelId,
            PartId = part.Id,
            LabelDate = DateTime.SpecifyKind(new DateTime(2025, 1, 1), DateTimeKind.Unspecified),
            Quantity = 100m,
            RemainingQuantity = 100m,
            Number = "00406",
            IsAssigned = true,
        };

        var receipt = new WipReceipt
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = section10.Id,
            OpNumber = 10,
            ReceiptDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 100m,
            UserId = Guid.NewGuid(),
            WipLabelId = labelId,
        };

        label.WipReceipt = receipt;

        dbContext.WipLabels.Add(label);
        dbContext.WipReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService());

        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, labelId, null),
        });

        var secondSummary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 20, 30, DateTime.UtcNow, 40m, null, labelId, null),
        });

        Assert.Equal(1, secondSummary.Saved);
        var transfer = Assert.Single(secondSummary.Items);
        Assert.Equal(labelId, transfer.WipLabelId);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithLabelOnNextOperation_UsesOperationBalanceWhenRemainingIsZero()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var section30 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 30" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };
        var operation30 = new Operation { Id = Guid.NewGuid(), Name = "030" };
        var labelId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section20, section30);
        dbContext.Operations.AddRange(operation20, operation30);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section20.Id, operation20.Id, 20),
            CreateRoute(part.Id, section30.Id, operation30.Id, 30));

        dbContext.WipBalances.AddRange(
            new WipBalance
            {
                Id = Guid.NewGuid(),
                PartId = part.Id,
                SectionId = section20.Id,
                OpNumber = 20,
                Quantity = 120m,
            },
            new WipBalance
            {
                Id = Guid.NewGuid(),
                PartId = part.Id,
                SectionId = section30.Id,
                OpNumber = 30,
                Quantity = 0m,
            });

        var label = new WipLabel
        {
            Id = labelId,
            PartId = part.Id,
            LabelDate = DateTime.SpecifyKind(new DateTime(2025, 1, 1), DateTimeKind.Unspecified),
            Quantity = 120m,
            RemainingQuantity = 0m,
            Number = "00405",
            IsAssigned = true,
        };

        var receipt = new WipReceipt
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = section20.Id,
            OpNumber = 20,
            ReceiptDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 120m,
            UserId = Guid.NewGuid(),
            WipLabelId = labelId,
        };

        label.WipReceipt = receipt;

        dbContext.WipLabels.Add(label);
        dbContext.WipReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService());

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 20, 30, DateTime.UtcNow, 120m, null, labelId, null),
        });

        Assert.Equal(1, summary.Saved);
        var storedLabel = await dbContext.WipLabels.SingleAsync(x => x.Id == labelId);
        Assert.Equal(0m, storedLabel.RemainingQuantity);

        var transfer = Assert.Single(summary.Items);
        Assert.Equal(labelId, transfer.WipLabelId);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_TwoLabelsOnSameOperation_Succeeds()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var toSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var toOperation = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, toSection);
        dbContext.Operations.AddRange(fromOperation, toOperation);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10),
            CreateRoute(part.Id, toSection.Id, toOperation.Id, 20));

        dbContext.WipBalances.AddRange(
            new WipBalance
            {
                Id = Guid.NewGuid(),
                PartId = part.Id,
                SectionId = fromSection.Id,
                OpNumber = 10,
                Quantity = 120m,
            },
            new WipBalance
            {
                Id = Guid.NewGuid(),
                PartId = part.Id,
                SectionId = toSection.Id,
                OpNumber = 20,
                Quantity = 0m,
            });

        var labels = new[]
        {
            CreateLabelWithReceipt(part.Id, fromSection.Id, 10, "00010", 60m),
            CreateLabelWithReceipt(part.Id, fromSection.Id, 10, "00011", 60m),
        };

        await dbContext.WipLabels.AddRangeAsync(labels.Select(x => x.Label));
        await dbContext.WipReceipts.AddRangeAsync(labels.Select(x => x.Receipt));

        await dbContext.SaveChangesAsync();

        var routeService = new RouteService(dbContext);
        var currentUser = new TestCurrentUserService();
        var service = new TransferService(dbContext, routeService, currentUser);
        var transferDate = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 50m, null, labels[0].Label.Id, null),
            new TransferItemDto(part.Id, 10, 20, transferDate, 40m, null, labels[1].Label.Id, null),
        });

        Assert.Equal(2, summary.Saved);

        var storedLabels = await dbContext.WipLabels
            .Where(x => x.PartId == part.Id)
            .OrderBy(x => x.Number)
            .ToListAsync();

        Assert.Equal(60m, storedLabels[0].RemainingQuantity);
        Assert.Equal(60m, storedLabels[1].RemainingQuantity);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_CreatesSingleToBalance_WhenMissingAndUsedTwiceInBatch()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 70" };
        var toSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 75" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "070" };
        var toOperation = new Operation { Id = Guid.NewGuid(), Name = "075" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, toSection);
        dbContext.Operations.AddRange(fromOperation, toOperation);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 70),
            CreateRoute(part.Id, toSection.Id, toOperation.Id, 75));

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 70,
            Quantity = 240m,
        });

        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService());
        var transferDate = new DateTime(2026, 2, 25, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 70, 75, transferDate, 120m, null, null, null),
            new TransferItemDto(part.Id, 70, 75, transferDate, 120m, null, null, null),
        });

        Assert.Equal(2, summary.Saved);

        var toBalances = await dbContext.WipBalances
            .Where(x => x.PartId == part.Id && x.SectionId == toSection.Id && x.OpNumber == 75)
            .ToListAsync();

        var toBalance = Assert.Single(toBalances);
        Assert.Equal(240m, toBalance.Quantity);
    }

    [Fact]
    public async Task RevertTransferAsync_RestoresBalancesAndMarksAudit()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var toSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var toOperation = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(fromSection, toSection);
        dbContext.Operations.AddRange(fromOperation, toOperation);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10),
            CreateRoute(part.Id, toSection.Id, toOperation.Id, 20));

        var fromBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 90m,
        };

        var toBalance = new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = toSection.Id,
            OpNumber = 20,
            Quantity = 5m,
        };

        dbContext.WipBalances.AddRange(fromBalance, toBalance);

        var initialFrom = fromBalance.Quantity;
        var initialTo = toBalance.Quantity;

        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService());
        var transferDate = new DateTime(2025, 6, 6, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 30m, null, null, null),
        });

        var audit = await dbContext.TransferAudits.SingleAsync();
        Assert.Equal(initialFrom - 30m, audit.FromBalanceAfter);
        Assert.False(audit.IsReverted);

        var revertResult = await service.RevertTransferAsync(audit.Id);

        Assert.Equal(initialFrom, revertResult.FromBalanceAfter);
        Assert.Equal(initialTo, revertResult.ToBalanceAfter);
        Assert.Empty(dbContext.WipTransfers);

        var restoredFrom = await dbContext.WipBalances.SingleAsync(x => x.Id == fromBalance.Id);
        var restoredTo = await dbContext.WipBalances.SingleAsync(x => x.Id == toBalance.Id);
        Assert.Equal(initialFrom, restoredFrom.Quantity);
        Assert.Equal(initialTo, restoredTo.Quantity);

        var updatedAudit = await dbContext.TransferAudits.SingleAsync();
        Assert.True(updatedAudit.IsReverted);
        Assert.NotNull(updatedAudit.RevertedAt);
    }

    private static (WipLabel Label, WipReceipt Receipt) CreateLabelWithReceipt(Guid partId, Guid sectionId, int opNumber, string number, decimal quantity)
    {
        var labelId = Guid.NewGuid();
        var label = new WipLabel
        {
            Id = labelId,
            PartId = partId,
            LabelDate = DateTime.SpecifyKind(new DateTime(2025, 1, 1), DateTimeKind.Unspecified),
            Quantity = quantity,
            RemainingQuantity = quantity,
            Number = number,
            IsAssigned = true,
        };

        var receipt = new WipReceipt
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = opNumber,
            ReceiptDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = quantity,
            UserId = Guid.NewGuid(),
            WipLabelId = labelId,
        };

        label.WipReceipt = receipt;

        return (label, receipt);
    }

    private static PartRoute CreateRoute(Guid partId, Guid sectionId, Guid operationId, int opNumber)
    {
        return new PartRoute
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OperationId = operationId,
            OpNumber = opNumber,
            NormHours = 1m,
        };
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
        public Guid UserId { get; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }
}
