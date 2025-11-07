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
        const string labelNumber = "LBL-001";

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

        var storedLabel = await dbContext.WipLabels.SingleAsync();
        Assert.True(storedLabel.IsAssigned);

        var receipt = await dbContext.WipReceipts.SingleAsync();
        Assert.Equal(labelId, receipt.WipLabelId);
        Assert.Equal(receiptQuantity, receipt.Quantity);
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
