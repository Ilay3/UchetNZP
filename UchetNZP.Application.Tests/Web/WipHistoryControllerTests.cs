using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Controllers;
using UchetNZP.Web.Models;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class WipHistoryControllerTests
{
    [Fact]
    public async Task Index_FiltersByPartAndSection()
    {
        await using var dbContext = CreateContext();

        var matchingPartId = Guid.NewGuid();
        var otherPartId = Guid.NewGuid();
        var sectionMatchId = Guid.NewGuid();
        var sectionOtherId = Guid.NewGuid();
        var targetSectionId = Guid.NewGuid();

        var matchingPart = new Part
        {
            Id = matchingPartId,
            Name = "Корпус редуктора",
            Code = "PRT-001",
        };

        var otherPart = new Part
        {
            Id = otherPartId,
            Name = "Вал привода",
            Code = "PRT-002",
        };

        var sectionMatch = new Section
        {
            Id = sectionMatchId,
            Name = "Сборочный участок",
            Code = "SB-01",
        };

        var sectionOther = new Section
        {
            Id = sectionOtherId,
            Name = "Мехобработка",
            Code = "MH-02",
        };

        var targetSection = new Section
        {
            Id = targetSectionId,
            Name = "Контроль качества",
            Code = "QC-03",
        };

        dbContext.Parts.AddRange(matchingPart, otherPart);
        dbContext.Sections.AddRange(sectionMatch, sectionOther, targetSection);

        var currentDate = DateTime.UtcNow;

        dbContext.WipLaunches.AddRange(
            new WipLaunch
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                PartId = matchingPartId,
                SectionId = sectionMatchId,
                FromOpNumber = 10,
                LaunchDate = currentDate,
                CreatedAt = currentDate,
                Quantity = 5m,
                SumHoursToFinish = 1.5m,
            },
            new WipLaunch
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                PartId = otherPartId,
                SectionId = sectionOtherId,
                FromOpNumber = 15,
                LaunchDate = currentDate,
                CreatedAt = currentDate,
                Quantity = 7m,
                SumHoursToFinish = 2m,
            });

        dbContext.WipReceipts.AddRange(
            new WipReceipt
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                PartId = matchingPartId,
                SectionId = sectionMatchId,
                OpNumber = 20,
                ReceiptDate = currentDate,
                CreatedAt = currentDate,
                Quantity = 3m,
            },
            new WipReceipt
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                PartId = otherPartId,
                SectionId = sectionOtherId,
                OpNumber = 30,
                ReceiptDate = currentDate,
                CreatedAt = currentDate,
                Quantity = 4m,
            });

        dbContext.WipTransfers.AddRange(
            new WipTransfer
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                PartId = matchingPartId,
                FromSectionId = sectionMatchId,
                FromOpNumber = 25,
                ToSectionId = targetSectionId,
                ToOpNumber = 30,
                TransferDate = currentDate,
                CreatedAt = currentDate,
                Quantity = 2m,
                Comment = "Передача на контроль",
            },
            new WipTransfer
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                PartId = otherPartId,
                FromSectionId = sectionOtherId,
                FromOpNumber = 35,
                ToSectionId = sectionOtherId,
                ToOpNumber = 40,
                TransferDate = currentDate,
                CreatedAt = currentDate,
                Quantity = 6m,
            });

        await dbContext.SaveChangesAsync();

        var controller = new WipHistoryController(dbContext);

        var query = new WipHistoryQuery
        {
            Part = "редукт",
            Section = "сбороч",
        };

        var actionResult = await controller.Index(query, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(actionResult);
        var model = Assert.IsType<WipHistoryViewModel>(viewResult.Model);

        Assert.True(model.HasData);
        Assert.Equal("редукт", model.Filter.PartSearch);
        Assert.Equal("сбороч", model.Filter.SectionSearch);

        var entries = model.Groups.SelectMany(group => group.Entries).ToList();

        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.Contains("Корпус редуктора", entry.PartDisplayName, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(entry.SectionName))
            {
                Assert.Contains("Сборочный участок", entry.SectionName, StringComparison.OrdinalIgnoreCase);
            }

            if (entry.HasTargetSection)
            {
                Assert.Contains("Контроль качества", entry.TargetSectionName, StringComparison.OrdinalIgnoreCase);
            }
        });

        Assert.DoesNotContain(entries, entry => entry.PartDisplayName.Contains("Вал привода", StringComparison.OrdinalIgnoreCase));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}
