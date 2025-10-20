using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;
using UchetNZP.Web.Controllers;
using UchetNZP.Web.Models;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class WipLaunchesControllerTests
{
    [Fact]
    public async Task Delete_ReturnsForbid_WhenUserNotAuthenticated()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_ForEmptyId()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        Authenticate(controller);

        var result = await controller.Delete(Guid.Empty, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Некорректный идентификатор запуска.", badRequest.Value);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenLaunchMissing()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext);
        Authenticate(controller);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var message = Assert.IsType<string>(notFound.Value);
        Assert.Contains("не найден", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenBalanceMissing()
    {
        await using var dbContext = CreateContext();
        var launch = new WipLaunch
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PartId = Guid.NewGuid(),
            SectionId = Guid.NewGuid(),
            FromOpNumber = 10,
            LaunchDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 5m,
            SumHoursToFinish = 1m,
        };

        dbContext.WipLaunches.Add(launch);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        Authenticate(controller);

        var result = await controller.Delete(launch.Id, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var message = Assert.IsType<string>(conflict.Value);
        Assert.Contains("Остаток НЗП", message);
    }

    [Fact]
    public async Task Delete_ReturnsOk_WhenLaunchRemoved()
    {
        await using var dbContext = CreateContext();
        var partId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var launchId = Guid.NewGuid();

        dbContext.WipBalances.Add(new WipBalance
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            OpNumber = 20,
            Quantity = 12m,
        });

        dbContext.WipLaunches.Add(new WipLaunch
        {
            Id = launchId,
            UserId = Guid.NewGuid(),
            PartId = partId,
            SectionId = sectionId,
            FromOpNumber = 20,
            LaunchDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Quantity = 8m,
            SumHoursToFinish = 3m,
        });

        dbContext.WipLaunchOperations.Add(new WipLaunchOperation
        {
            Id = Guid.NewGuid(),
            WipLaunchId = launchId,
            OperationId = Guid.NewGuid(),
            SectionId = sectionId,
            OpNumber = 25,
            Quantity = 8m,
            Hours = 1.6m,
            NormHours = 0.2m,
        });

        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        Authenticate(controller);

        var actionResult = await controller.Delete(launchId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<LaunchDeleteResponseModel>(okResult.Value);
        Assert.Equal(launchId, response.LaunchId);
        Assert.Equal(OperationNumber.Format(20), response.FromOperation);
        Assert.Equal(20m, response.Remaining);
        Assert.Contains("успешно", response.Message, StringComparison.OrdinalIgnoreCase);

        Assert.False(await dbContext.WipLaunches.AnyAsync());
        Assert.False(await dbContext.WipLaunchOperations.AnyAsync());
        var balance = await dbContext.WipBalances.SingleAsync();
        Assert.Equal(20m, balance.Quantity);
    }

    private static WipLaunchesController CreateController(AppDbContext dbContext)
    {
        var routeService = new RouteService(dbContext);
        var currentUser = new TestCurrentUserService();
        var launchService = new LaunchService(dbContext, routeService, currentUser);
        var reportService = new FakeReportService();
        var controller = new WipLaunchesController(dbContext, launchService, routeService, reportService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static void Authenticate(Controller controller)
    {
        var httpContext = controller.HttpContext;
        if (httpContext is null)
        {
            throw new InvalidOperationException("HTTP контекст не инициализирован.");
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "TestAuth");

        httpContext.User = new ClaimsPrincipal(identity);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeReportService : IReportService
    {
        public Task<byte[]> ExportLaunchesToExcelAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> ExportLaunchesByDateAsync(DateTime date, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<byte[]> ExportRoutesToExcelAsync(string? search, Guid? sectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    }
}
