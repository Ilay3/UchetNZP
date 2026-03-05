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
        var service = new TransferService(dbContext, routeService, currentUser, new LabelNumberingService(dbContext));

        var transferDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 40m, "", null, TransferScenario.MoveLabel, false, null, null)
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
        var service = new TransferService(dbContext, routeService, currentUser, new LabelNumberingService(dbContext));

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
                TransferScenario.MoveLabel,
                false,
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

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));

        var transferDate = new DateTime(2025, 2, 2, 0, 0, 0, DateTimeKind.Utc);
        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, WarehouseDefaults.OperationNumber, transferDate, 40m, "Склад", null, TransferScenario.MoveLabel, false, null, null),
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
    public async Task AddTransfersBatchAsync_ToWarehouse_WithLegacyNumber_CreatesWarehouseReferences()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var fromSection = new Section { Id = Guid.NewGuid(), Name = "Вид работ" };
        var fromOperation = new Operation { Id = Guid.NewGuid(), Name = "010" };

        dbContext.Parts.Add(part);
        dbContext.Sections.Add(fromSection);
        dbContext.Operations.Add(fromOperation);
        dbContext.PartRoutes.Add(CreateRoute(part.Id, fromSection.Id, fromOperation.Id, 10));

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = part.Id,
            SectionId = fromSection.Id,
            OpNumber = 10,
            Quantity = 50m,
        });

        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, WarehouseDefaults.LegacyOperationNumber, DateTime.UtcNow, 20m, null, null, TransferScenario.MoveLabel, false, null, null),
        });

        var item = Assert.Single(summary.Items);
        Assert.Equal(WarehouseDefaults.OperationNumber, item.ToOpNumber);
        Assert.Equal(WarehouseDefaults.SectionId, item.ToSectionId);

        var transferOperation = await dbContext.WipTransferOperations
            .Where(x => x.WipTransferId == item.TransferId && x.QuantityChange > 0)
            .SingleAsync();

        Assert.Equal(WarehouseDefaults.OperationId, transferOperation.OperationId);
        Assert.Equal(WarehouseDefaults.SectionId, transferOperation.SectionId);

        Assert.NotNull(await dbContext.Operations.FindAsync(WarehouseDefaults.OperationId));
        Assert.NotNull(await dbContext.Sections.FindAsync(WarehouseDefaults.SectionId));
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

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        var transferDate = new DateTime(2025, 3, 3, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 40m, null, labelId, TransferScenario.MoveLabel, false, null, null),
        });

        Assert.Equal(1, summary.Saved);
        var storedLabel = await dbContext.WipLabels.SingleAsync(x => x.Id == labelId);
        Assert.Equal(100m, storedLabel.RemainingQuantity);

        var transfer = await dbContext.WipTransfers.SingleAsync();
        Assert.Equal(labelId, transfer.WipLabelId);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithResidualLabelSplit_CreatesChildLabelAndKeepsSourceIdentity()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };
        var labelId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));

        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var label = new WipLabel { Id = labelId, PartId = part.Id, LabelDate = DateTime.UtcNow, Quantity = 100m, RemainingQuantity = 100m, Number = "103", IsAssigned = true };
        var receipt = new WipReceipt { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, ReceiptDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, Quantity = 100m, UserId = Guid.NewGuid(), WipLabelId = labelId };
        label.WipReceipt = receipt;
        dbContext.WipLabels.Add(label);
        dbContext.WipReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, labelId, TransferScenario.SplitAndTransfer, true, null, null),
        });

        var source = await dbContext.WipLabels.SingleAsync(x => x.Id == labelId);
        var child = await dbContext.WipLabels.SingleAsync(x => x.PartId == part.Id && x.Id != labelId);
        Assert.Equal("103", source.Number);
        Assert.Equal(60m, source.RemainingQuantity);
        Assert.Equal("103/1", child.Number);
        Assert.Equal(40m, child.Quantity);
        var audit = await dbContext.TransferAudits.SingleAsync();
        Assert.Equal("103/1", audit.ResidualLabelNumber);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithRepeatedResidualLabelSplit_CreatesSlashTwoLabel()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };
        var labelId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));

        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var label = new WipLabel { Id = labelId, PartId = part.Id, LabelDate = DateTime.UtcNow, Quantity = 100m, RemainingQuantity = 100m, Number = "103", IsAssigned = true };
        var receipt = new WipReceipt { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, ReceiptDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, Quantity = 100m, UserId = Guid.NewGuid(), WipLabelId = labelId };
        label.WipReceipt = receipt;
        dbContext.WipLabels.AddRange(label, new WipLabel { Id = Guid.NewGuid(), PartId = part.Id, LabelDate = DateTime.UtcNow, Quantity = 10m, RemainingQuantity = 10m, Number = "103/1", IsAssigned = true });
        dbContext.WipReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, labelId, TransferScenario.SplitAndTransfer, true, null, null),
        });

        var residual = await dbContext.WipLabels.SingleAsync(x => x.PartId == part.Id && x.Number == "103/2");
        Assert.Equal(40m, residual.Quantity);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithSlashLabelSplit_UsesNextSuffixForTransferredChild()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };
        var labelId = Guid.NewGuid();

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));

        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var label = new WipLabel { Id = labelId, PartId = part.Id, LabelDate = DateTime.UtcNow, Quantity = 100m, RemainingQuantity = 100m, Number = "103/3", IsAssigned = true };
        var receipt = new WipReceipt { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, ReceiptDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, Quantity = 100m, UserId = Guid.NewGuid(), WipLabelId = labelId };
        label.WipReceipt = receipt;
        dbContext.WipLabels.Add(label);
        dbContext.WipReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, labelId, TransferScenario.SplitAndTransfer, true, null, null),
        });

        var transfer = await dbContext.WipTransfers.Include(x => x.WipLabel).SingleAsync();
        Assert.Equal("103/4", transfer.WipLabel!.Number);

        var source = await dbContext.WipLabels
            .SingleAsync(x => x.PartId == part.Id && x.Id != transfer.WipLabelId);
        Assert.Equal("103/3", source.Number);
        Assert.Equal(60m, source.RemainingQuantity);

        var item = Assert.Single(summary.Items);
        Assert.Equal("103/4", item.LabelNumber);
        Assert.Equal("103/4", item.ResidualLabelNumber);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithTwoSplitsInBatch_AvoidsResidualNumberConflict()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));

        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 200m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var firstLabelId = Guid.NewGuid();
        var secondLabelId = Guid.NewGuid();
        var firstLabel = new WipLabel { Id = firstLabelId, PartId = part.Id, LabelDate = DateTime.UtcNow, Quantity = 100m, RemainingQuantity = 100m, Number = "103-A", IsAssigned = true };
        var secondLabel = new WipLabel { Id = secondLabelId, PartId = part.Id, LabelDate = DateTime.UtcNow, Quantity = 100m, RemainingQuantity = 100m, Number = "103-B", IsAssigned = true };
        var firstReceipt = new WipReceipt { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, ReceiptDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, Quantity = 100m, UserId = Guid.NewGuid(), WipLabelId = firstLabelId };
        var secondReceipt = new WipReceipt { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, ReceiptDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, Quantity = 100m, UserId = Guid.NewGuid(), WipLabelId = secondLabelId };
        firstLabel.WipReceipt = firstReceipt;
        secondLabel.WipReceipt = secondReceipt;
        dbContext.WipLabels.AddRange(firstLabel, secondLabel);
        dbContext.WipReceipts.AddRange(firstReceipt, secondReceipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, firstLabelId, TransferScenario.SplitAndTransfer, true, 103, null),
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 30m, null, secondLabelId, TransferScenario.SplitAndTransfer, true, 103, null),
        });

        var generated = await dbContext.WipLabels
            .Where(x => x.PartId == part.Id && x.Id != firstLabelId && x.Id != secondLabelId)
            .Select(x => x.Number)
            .OrderBy(x => x)
            .ToListAsync();

        Assert.Contains("103/1", generated);
        Assert.Contains("103/2", generated);
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

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        var transferDate = new DateTime(2025, 5, 5, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, WarehouseDefaults.OperationNumber, transferDate, 30m, null, labelId, TransferScenario.MoveLabel, false, null, null),
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

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));

        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, labelId, TransferScenario.MoveLabel, false, null, null),
        });

        var secondSummary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 20, 30, DateTime.UtcNow, 40m, null, labelId, TransferScenario.MoveLabel, false, null, null),
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

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 20, 30, DateTime.UtcNow, 120m, null, labelId, TransferScenario.MoveLabel, false, null, null),
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
        var service = new TransferService(dbContext, routeService, currentUser, new LabelNumberingService(dbContext));
        var transferDate = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 50m, null, labels[0].Label.Id, TransferScenario.MoveLabel, false, null, null),
            new TransferItemDto(part.Id, 10, 20, transferDate, 40m, null, labels[1].Label.Id, TransferScenario.MoveLabel, false, null, null),
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

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        var transferDate = new DateTime(2026, 2, 25, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 70, 75, transferDate, 120m, null, null, TransferScenario.MoveLabel, false, null, null),
            new TransferItemDto(part.Id, 70, 75, transferDate, 120m, null, null, TransferScenario.MoveLabel, false, null, null),
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

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        var transferDate = new DateTime(2025, 6, 6, 0, 0, 0, DateTimeKind.Utc);

        var summary = await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, transferDate, 30m, null, null, TransferScenario.MoveLabel, false, null, null),
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

    [Fact]
    public async Task AddTransfersBatchAsync_WithLabel_UpdatesLabelCurrentLocation()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));

        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var setup = CreateLabelWithReceipt(part.Id, section10.Id, 10, "221", 100m);
        dbContext.WipLabels.Add(setup.Label);
        dbContext.WipReceipts.Add(setup.Receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, setup.Label.Id, TransferScenario.MoveLabel, false, null, null),
        });

        var storedLabel = await dbContext.WipLabels.SingleAsync(x => x.Id == setup.Label.Id);
        Assert.Equal(section20.Id, storedLabel.CurrentSectionId);
        Assert.Equal(20, storedLabel.CurrentOpNumber);
        Assert.Equal(WipLabelStatus.Active, storedLabel.Status);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithResidualLabel_StoresParentAndRoot()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));

        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var setup = CreateLabelWithReceipt(part.Id, section10.Id, 10, "330", 100m);
        dbContext.WipLabels.Add(setup.Label);
        dbContext.WipReceipts.Add(setup.Receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, setup.Label.Id, TransferScenario.SplitAndTransfer, true, null, null),
        });

        var source = await dbContext.WipLabels.SingleAsync(x => x.Id == setup.Label.Id);
        var residual = await dbContext.WipLabels.SingleAsync(x => x.Id != setup.Label.Id);

        Assert.Equal(source.Id, residual.ParentLabelId);
        var expectedRootId = source.RootLabelId == Guid.Empty ? source.Id : source.RootLabelId;
        Assert.Equal(expectedRootId, residual.RootLabelId);
        Assert.Equal("330", residual.RootNumber);
        Assert.Equal(1, residual.Suffix);
        Assert.Equal(section20.Id, residual.CurrentSectionId);
        Assert.Equal(20, residual.CurrentOpNumber);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithMoveLabelScenario_WritesMoveLedgerEvent()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));
        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var setup = CreateLabelWithReceipt(part.Id, section10.Id, 10, "500", 100m);
        dbContext.WipLabels.Add(setup.Label);
        dbContext.WipReceipts.Add(setup.Receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, setup.Label.Id, TransferScenario.MoveLabel, false, null, null),
        });

        var ledger = await dbContext.WipLabelLedger.ToListAsync();
        Assert.Contains(ledger, x => x.EventType == WipLabelEventType.Move);
        Assert.DoesNotContain(ledger, x => x.EventType == WipLabelEventType.Split);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithSplitAndTransferScenario_WritesSplitLedgerEvent()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));
        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var setup = CreateLabelWithReceipt(part.Id, section10.Id, 10, "501", 100m);
        dbContext.WipLabels.Add(setup.Label);
        dbContext.WipReceipts.Add(setup.Receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, setup.Label.Id, TransferScenario.SplitAndTransfer, true, null, null),
        });

        var ledger = await dbContext.WipLabelLedger.ToListAsync();
        Assert.Contains(ledger, x => x.EventType == WipLabelEventType.Transfer);
        Assert.Contains(ledger, x => x.EventType == WipLabelEventType.Split);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithSplitAndTransferScenario_WritesTransferLabelUsage()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));
        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var setup = CreateLabelWithReceipt(part.Id, section10.Id, 10, "700", 100m);
        dbContext.WipLabels.Add(setup.Label);
        dbContext.WipReceipts.Add(setup.Receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, setup.Label.Id, TransferScenario.SplitAndTransfer, true, null, null),
        });

        var usage = await dbContext.TransferLabelUsages.SingleAsync();
        Assert.Equal(40m, usage.Qty);
        Assert.Equal(0m, usage.ScrapQty);
        Assert.Equal(100m, usage.RemainingBefore);
        Assert.True(usage.Qty + usage.ScrapQty <= usage.RemainingBefore);
        Assert.Equal(setup.Label.Id, usage.FromLabelId);
        Assert.NotNull(usage.CreatedToLabelId);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithMoveLabelScenario_WritesTransferLabelUsageWithoutCreatedLabel()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));
        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 100m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var setup = CreateLabelWithReceipt(part.Id, section10.Id, 10, "701", 100m);
        dbContext.WipLabels.Add(setup.Label);
        dbContext.WipReceipts.Add(setup.Receipt);
        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, setup.Label.Id, TransferScenario.MoveLabel, false, null, null),
        });

        var usage = await dbContext.TransferLabelUsages.SingleAsync();
        Assert.Equal(40m, usage.Qty);
        Assert.Equal(0m, usage.ScrapQty);
        Assert.Equal(100m, usage.RemainingBefore);
        Assert.True(usage.Qty + usage.ScrapQty <= usage.RemainingBefore);
        Assert.Equal(setup.Label.Id, usage.FromLabelId);
        Assert.Null(usage.CreatedToLabelId);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithMoveLabelScenario_AllowsLabelWithoutReceipt()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));
        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 40m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var labelId = Guid.NewGuid();
        dbContext.WipLabels.Add(new WipLabel
        {
            Id = labelId,
            PartId = part.Id,
            LabelDate = DateTime.SpecifyKind(new DateTime(2025, 1, 1), DateTimeKind.Unspecified),
            Quantity = 40m,
            RemainingQuantity = 40m,
            Number = "00001/1",
            IsAssigned = true,
            Status = WipLabelStatus.Active,
            CurrentSectionId = section10.Id,
            CurrentOpNumber = 10,
            RootLabelId = labelId,
            RootNumber = "00001",
            Suffix = 1,
        });

        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));
        await service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 40m, null, labelId, TransferScenario.MoveLabel, false, null, null),
        });

        var movedLabel = await dbContext.WipLabels.SingleAsync(x => x.Id == labelId);
        Assert.Equal(section20.Id, movedLabel.CurrentSectionId);
        Assert.Equal(20, movedLabel.CurrentOpNumber);
    }

    [Fact]
    public async Task AddTransfersBatchAsync_WithSplitAndTransferScenario_RejectsLabelWithoutReceipt()
    {
        await using var dbContext = CreateContext();

        var part = new Part { Id = Guid.NewGuid(), Name = "Деталь" };
        var section10 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 10" };
        var section20 = new Section { Id = Guid.NewGuid(), Name = "Вид работ 20" };
        var operation10 = new Operation { Id = Guid.NewGuid(), Name = "010" };
        var operation20 = new Operation { Id = Guid.NewGuid(), Name = "020" };

        dbContext.Parts.Add(part);
        dbContext.Sections.AddRange(section10, section20);
        dbContext.Operations.AddRange(operation10, operation20);
        dbContext.PartRoutes.AddRange(
            CreateRoute(part.Id, section10.Id, operation10.Id, 10),
            CreateRoute(part.Id, section20.Id, operation20.Id, 20));
        dbContext.WipBalances.AddRange(
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section10.Id, OpNumber = 10, Quantity = 40m },
            new WipBalance { Id = Guid.NewGuid(), PartId = part.Id, SectionId = section20.Id, OpNumber = 20, Quantity = 0m });

        var labelId = Guid.NewGuid();
        dbContext.WipLabels.Add(new WipLabel
        {
            Id = labelId,
            PartId = part.Id,
            LabelDate = DateTime.SpecifyKind(new DateTime(2025, 1, 1), DateTimeKind.Unspecified),
            Quantity = 40m,
            RemainingQuantity = 40m,
            Number = "00001/1",
            IsAssigned = true,
            Status = WipLabelStatus.Active,
            CurrentSectionId = section10.Id,
            CurrentOpNumber = 10,
            RootLabelId = labelId,
            RootNumber = "00001",
            Suffix = 1,
        });

        await dbContext.SaveChangesAsync();

        var service = new TransferService(dbContext, new RouteService(dbContext), new TestCurrentUserService(), new LabelNumberingService(dbContext));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddTransfersBatchAsync(new[]
        {
            new TransferItemDto(part.Id, 10, 20, DateTime.UtcNow, 20m, null, labelId, TransferScenario.SplitAndTransfer, true, null, null),
        }));

        Assert.Contains("не связан с приходом", ex.Message);
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
            Status = WipLabelStatus.Active,
            CurrentSectionId = sectionId,
            CurrentOpNumber = opNumber,
            RootLabelId = labelId,
            ParentLabelId = null,
            RootNumber = WipLabelInvariants.ParseNumber(number).RootNumber,
            Suffix = WipLabelInvariants.ParseNumber(number).Suffix,
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
