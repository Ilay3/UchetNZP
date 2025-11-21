using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Controllers;
using UchetNZP.Web.Models;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class WipReceiptsControllerTests
{
    [Fact]
    public async Task Revert_ReturnsRestoredReceipt()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        const int opNumber = 9;
        const decimal quantity = 2m;
        const string labelNumber = "00003";

        dbContext.Parts.Add(new Part
        {
            Id = partId,
            Name = "Корпус",
            Code = "PRT-10",
        });

        dbContext.Sections.Add(new Section
        {
            Id = sectionId,
            Name = "Участок",
        });

        dbContext.Operations.Add(new Operation
        {
            Id = operationId,
            Name = "Сверловка",
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
            LabelDate = DateTime.SpecifyKind(new DateTime(2024, 6, 1), DateTimeKind.Unspecified),
            Quantity = quantity,
            RemainingQuantity = quantity,
            Number = labelNumber,
            IsAssigned = false,
        });

        await dbContext.SaveChangesAsync();

        var service = new WipService(dbContext, new TestCurrentUserService());
        var receiptDate = DateTime.SpecifyKind(new DateTime(2024, 6, 1), DateTimeKind.Unspecified);

        var saveResult = await service.AddReceiptsBatchAsync(
            new[] { new Application.Contracts.Wip.ReceiptItemDto(partId, opNumber, sectionId, receiptDate, quantity, null, labelId, labelNumber, true) },
            CancellationToken.None);

        var receiptInfo = Assert.Single(saveResult.Items);

        await service.DeleteReceiptAsync(receiptInfo.ReceiptId);

        var controller = new WipReceiptsController(dbContext, service);

        var actionResult = await controller.Revert(receiptInfo.ReceiptId, new WipReceiptsController.ReceiptRevertRequest(receiptInfo.VersionId), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var viewModel = Assert.IsType<ReceiptRevertResultViewModel>(okResult.Value);

        Assert.Equal(quantity, viewModel.TargetQuantity);
        Assert.NotEqual(Guid.Empty, viewModel.VersionId);
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
