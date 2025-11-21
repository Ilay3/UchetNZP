using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class HistoryPageUiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HistoryPageUiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("HistoryUiTests"));
            });
        });
    }

    [Fact]
    public async Task HistoryPage_ShowsRollbackButtonsForReceiptAndTransfer()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();

            var partId = Guid.NewGuid();
            var fromSectionId = Guid.NewGuid();
            var toSectionId = Guid.NewGuid();
            var receiptSectionId = Guid.NewGuid();
            var operationId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            dbContext.Parts.Add(new Part
            {
                Id = partId,
                Name = "Кронштейн",
                Code = "PRT-100",
            });

            dbContext.Sections.AddRange(
                new Section
                {
                    Id = fromSectionId,
                    Name = "Сборка",
                    Code = "SB-1",
                },
                new Section
                {
                    Id = toSectionId,
                    Name = "Контроль",
                    Code = "QC-1",
                },
                new Section
                {
                    Id = receiptSectionId,
                    Name = "Приёмка",
                    Code = "RC-1",
                });

            dbContext.Operations.Add(new Operation
            {
                Id = operationId,
                Name = "Проверка",
                Code = "OP-10",
            });

            var receiptId = Guid.NewGuid();
            dbContext.WipReceipts.Add(new WipReceipt
            {
                Id = receiptId,
                UserId = Guid.NewGuid(),
                PartId = partId,
                SectionId = receiptSectionId,
                OpNumber = 10,
                ReceiptDate = now,
                CreatedAt = now,
                Quantity = 5m,
                Comment = "Тестовый приход",
            });

            dbContext.ReceiptAudits.Add(new ReceiptAudit
            {
                Id = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                ReceiptId = receiptId,
                PartId = partId,
                SectionId = receiptSectionId,
                OpNumber = 10,
                PreviousQuantity = 0m,
                NewQuantity = 5m,
                ReceiptDate = now,
                Comment = "Создан",
                PreviousBalance = 0m,
                NewBalance = 5m,
                PreviousLabelAssigned = false,
                NewLabelAssigned = true,
                UserId = Guid.NewGuid(),
                CreatedAt = now,
                Action = "Created",
            });

            var transferAuditId = Guid.NewGuid();
            var transferAudit = new TransferAudit
            {
                Id = transferAuditId,
                TransactionId = Guid.NewGuid(),
                TransferId = Guid.NewGuid(),
                PartId = partId,
                FromSectionId = fromSectionId,
                FromOpNumber = 10,
                ToSectionId = toSectionId,
                ToOpNumber = 20,
                Quantity = 2m,
                Comment = "Тестовая передача",
                TransferDate = now,
                CreatedAt = now,
                UserId = Guid.NewGuid(),
                FromBalanceBefore = 5m,
                FromBalanceAfter = 3m,
                ToBalanceBefore = 1m,
                ToBalanceAfter = 3m,
                IsWarehouseTransfer = false,
                LabelNumber = "12345",
                ScrapQuantity = 0m,
                IsReverted = false,
            };

            transferAudit.Operations.Add(new TransferAuditOperation
            {
                Id = Guid.NewGuid(),
                TransferAuditId = transferAuditId,
                SectionId = fromSectionId,
                OpNumber = 10,
                OperationId = operationId,
                BalanceBefore = 5m,
                BalanceAfter = 3m,
                QuantityChange = -2m,
                IsWarehouse = false,
            });

            transferAudit.Operations.Add(new TransferAuditOperation
            {
                Id = Guid.NewGuid(),
                TransferAuditId = transferAuditId,
                SectionId = toSectionId,
                OpNumber = 20,
                OperationId = operationId,
                BalanceBefore = 1m,
                BalanceAfter = 3m,
                QuantityChange = 2m,
                IsWarehouse = false,
            });

            dbContext.TransferAudits.Add(transferAudit);

            await dbContext.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/wip/history?types=receipt&types=transfer&from=2024-01-01&to=2026-12-31");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("js-history-receipt-revert", html, StringComparison.Ordinal);
        Assert.Contains("js-history-transfer-revert", html, StringComparison.Ordinal);
    }
}
