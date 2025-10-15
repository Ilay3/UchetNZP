using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Launches;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("wip/launch")]
public class WipLaunchesController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly ILaunchService _launchService;
    private readonly IRouteService _routeService;
    private readonly IReportService _reportService;

    public WipLaunchesController(
        AppDbContext dbContext,
        ILaunchService launchService,
        IRouteService routeService,
        IReportService reportService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _launchService = launchService ?? throw new ArgumentNullException(nameof(launchService));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View("~/Views/Wip/Launch.cshtml");
    }

    [HttpGet("parts")]
    public async Task<IActionResult> GetParts([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Parts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                (x.Code != null && x.Code.ToLower().Contains(term)));
        }

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpGet("operations")]
    public async Task<IActionResult> GetOperations([FromQuery] Guid partId, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty)
        {
            return BadRequest("Не выбрана деталь.");
        }

        var operationsQuery = from route in _dbContext.PartRoutes.AsNoTracking()
                              where route.PartId == partId
                              join operation in _dbContext.Operations.AsNoTracking() on route.OperationId equals operation.Id into operations
                              from operation in operations.DefaultIfEmpty()
                              join section in _dbContext.Sections.AsNoTracking() on route.SectionId equals section.Id into sections
                              from section in sections.DefaultIfEmpty()
                              join balance in _dbContext.WipBalances.AsNoTracking()
                                  on new { route.PartId, route.SectionId, route.OpNumber }
                                  equals new { balance.PartId, balance.SectionId, balance.OpNumber } into balances
                              from balance in balances.DefaultIfEmpty()
                              orderby route.OpNumber
                              select new LaunchOperationLookupViewModel(
                                  route.OpNumber,
                                  operation != null ? operation.Name : string.Empty,
                                  route.NormHours,
                                  route.SectionId,
                                  section != null ? section.Name : string.Empty,
                                  balance != null ? balance.Quantity : 0m);

        var operations = await operationsQuery
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(operations);
    }

    [HttpGet("tail")]
    public async Task<IActionResult> GetTail([FromQuery] Guid partId, [FromQuery] int opNumber, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty || opNumber <= 0)
        {
            return BadRequest("Недостаточно данных для расчёта хвоста маршрута.");
        }

        var tail = await _routeService.GetTailToFinishAsync(partId, opNumber.ToString(CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);

        var operations = tail
            .Select(x => new LaunchTailOperationViewModel(
                x.OpNumber,
                x.Operation != null ? x.Operation.Name : string.Empty,
                x.NormHours,
                x.SectionId,
                x.Section != null ? x.Section.Name : string.Empty))
            .ToList();

        var summary = new LaunchTailSummaryViewModel(operations, operations.Sum(x => x.NormHours));

        return Ok(summary);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] LaunchSaveRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("Список запусков пуст.");
        }

        var dtos = request.Items
            .Select(x => new LaunchItemDto(x.PartId, x.FromOpNumber, x.LaunchDate, x.Quantity, x.DocumentNumber))
            .ToList();

        var summary = await _launchService.AddLaunchesBatchAsync(dtos, cancellationToken).ConfigureAwait(false);

        var model = new LaunchBatchSummaryViewModel(
            summary.Saved,
            summary.Items
                .Select(x => new LaunchBatchItemViewModel(x.PartId, x.FromOpNumber, x.SectionId, x.Quantity, x.Remaining, x.SumHoursToFinish, x.LaunchId))
                .ToList());

        return Ok(model);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        var file = await _reportService.ExportLaunchesToExcelAsync(from, to, cancellationToken).ConfigureAwait(false);
        var fileName = $"Запуски_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public record LaunchSaveRequest(IReadOnlyList<LaunchSaveItem> Items);

    public record LaunchSaveItem(
        Guid PartId,
        int FromOpNumber,
        DateTime LaunchDate,
        decimal Quantity,
        string? DocumentNumber);
}
