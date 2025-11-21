using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class WipServiceTests
{
    [Fact]
    public async Task AddReceiptsBatchAsync_AssignsProvidedLabel()
    {
        await using var dbContext = createContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        const int opNumber = 15;
        const decimal initialQuantity = 5m;
        const decimal receiptQuantity = 3m;
        const string labelNumber = "00001";

        dbContext.Parts.Add(new Part
        {
            Id = partId,
            Name = "Деталь",
            Code = "DET-1",
        });

        dbContext.Sections.Add(new Section
        {
            Id = sectionId,
            Name = "Секция",
        });

        dbContext.Operations.Add(new Operation
        {
            Id = operationId,
            Name = "Операция",
        });

        dbContext.PartRoutes.Add(new PartRoute
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OperationId = operationId,
            OpNumber = opNumber,
            NormHours = 1m,
        });

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = opNumber,
            Quantity = initialQuantity,
        });

        dbContext.WipLabels.Add(new WipLabel
        {
            Id = labelId,
            PartId = partId,
            LabelDate = DateTime.SpecifyKind(new DateTime(2024, 1, 1), DateTimeKind.Unspecified),
            Quantity = receiptQuantity,
            Number = labelNumber,
            IsAssigned = false,
        });

        await dbContext.SaveChangesAsync();

        var service = new WipService(dbContext, new TestCurrentUserService());
        var receiptDate = DateTime.SpecifyKind(new DateTime(2024, 2, 1), DateTimeKind.Unspecified);

        var dto = new ReceiptItemDto(
            partId,
            opNumber,
            sectionId,
            receiptDate,
            receiptQuantity,
            "Примечание",
            labelId,
            labelNumber,
            false);

        var summary = await service.AddReceiptsBatchAsync(new[] { dto });

        Assert.Equal(1, summary.Saved);
        var savedItem = Assert.Single(summary.Items);
        Assert.Equal(labelId, savedItem.WipLabelId);
        Assert.Equal(labelNumber, savedItem.LabelNumber);
        Assert.True(savedItem.IsAssigned);
        Assert.NotEqual(Guid.Empty, savedItem.VersionId);

        var storedLabel = await dbContext.WipLabels.SingleAsync();
        Assert.True(storedLabel.IsAssigned);

        var receipt = await dbContext.WipReceipts.SingleAsync();
        Assert.Equal(labelId, receipt.WipLabelId);
        Assert.Equal(receiptQuantity, receipt.Quantity);

        Assert.Equal(1, await dbContext.ReceiptAudits.CountAsync());
    }

    [Fact]
    public async Task RevertReceiptAsync_RestoresDeletedReceipt()
    {
        await using var dbContext = createContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        const int opNumber = 7;
        const decimal receiptQuantity = 4m;
        const string labelNumber = "00002";

        dbContext.Parts.Add(new Part
        {
            Id = partId,
            Name = "Деталь",
            Code = "DTL-1",
        });

        dbContext.Sections.Add(new Section
        {
            Id = sectionId,
            Name = "Секция",
        });

        dbContext.Operations.Add(new Operation
        {
            Id = operationId,
            Name = "Операция",
        });

        dbContext.PartRoutes.Add(new PartRoute
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OperationId = operationId,
            OpNumber = opNumber,
            NormHours = 1m,
        });

        dbContext.WipLabels.Add(new WipLabel
        {
            Id = labelId,
            PartId = partId,
            LabelDate = DateTime.SpecifyKind(new DateTime(2024, 5, 5), DateTimeKind.Unspecified),
            Quantity = receiptQuantity,
            RemainingQuantity = receiptQuantity,
            Number = labelNumber,
            IsAssigned = false,
        });

        await dbContext.SaveChangesAsync();

        var service = new WipService(dbContext, new TestCurrentUserService());

        var receiptDate = DateTime.SpecifyKind(new DateTime(2024, 5, 5), DateTimeKind.Unspecified);
        var saveResult = await service.AddReceiptsBatchAsync(
            new[]
            {
                new ReceiptItemDto(partId, opNumber, sectionId, receiptDate, receiptQuantity, null, labelId, labelNumber, true),
            });

        var versionId = Assert.Single(saveResult.Items).VersionId;
        var receiptId = Assert.Single(saveResult.Items).ReceiptId;

        await service.DeleteReceiptAsync(receiptId);

        var revertResult = await service.RevertReceiptAsync(receiptId, versionId);

        var balance = await dbContext.WipBalances.SingleAsync();
        Assert.Equal(receiptQuantity, balance.Quantity);

        var restoredReceipt = await dbContext.WipReceipts.SingleAsync();
        Assert.Equal(receiptId, restoredReceipt.Id);
        Assert.Equal(receiptQuantity, restoredReceipt.Quantity);

        Assert.Equal(receiptQuantity, revertResult.TargetQuantity);
        Assert.NotEqual(Guid.Empty, revertResult.VersionId);
    }

    private static AppDbContext createContext()
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
