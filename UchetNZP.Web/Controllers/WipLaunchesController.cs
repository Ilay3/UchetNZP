using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Launches;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Shared;

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

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] LaunchHistoryQuery? query, CancellationToken cancellationToken)
    {
        var now = DateTime.Now.Date;
        var defaultFrom = now.AddDays(-13);
        var (fromDate, toDate) = NormalizePeriod(query?.From ?? defaultFrom, query?.To ?? now);

        var fromUtc = ToUtcStartOfDay(fromDate);
        var toUtcExclusive = ToUtcStartOfDay(toDate.AddDays(1));

        var launches = await _dbContext.WipLaunches
            .AsNoTracking()
            .Where(x => x.LaunchDate >= fromUtc && x.LaunchDate < toUtcExclusive)
            .Include(x => x.Part)
            .Include(x => x.Section)
            .Include(x => x.Operations)
                .ThenInclude(o => o.Operation)
            .Include(x => x.Operations)
                .ThenInclude(o => o.Section)
            .OrderBy(x => x.LaunchDate)
            .ThenBy(x => x.Part != null ? x.Part.Name : string.Empty)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = launches
            .Select(x => new LaunchHistoryItemViewModel(
                x.Id,
                ToLocalDateTime(x.LaunchDate),
                x.Part != null ? x.Part.Name : string.Empty,
                x.Part?.Code,
                x.Section != null ? x.Section.Name : string.Empty,
                OperationNumber.Format(x.FromOpNumber),
                x.Quantity,
                x.SumHoursToFinish,
                x.Comment,
                x.Operations
                    .OrderBy(o => o.OpNumber)
                    .Select(o => new LaunchHistoryOperationViewModel(
                        OperationNumber.Format(o.OpNumber),
                        o.Operation != null ? o.Operation.Name : string.Empty,
                        o.Section != null ? o.Section.Name : string.Empty,
                        o.NormHours,
                        o.Hours))
                    .ToList()))
            .ToList();

        var grouped = items
            .GroupBy(x => x.Date)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var launches = g.OrderBy(i => i.LaunchDate).ToList();
                var sectionSummaries = launches
                    .SelectMany(i => i.Operations)
                    .GroupBy(op => op.SectionName)
                    .Select(opGroup => new LaunchHistorySectionSummaryViewModel(
                        opGroup.Key,
                        opGroup.Sum(o => o.Hours)))
                    .OrderByDescending(x => x.Hours)
                    .ThenBy(x => x.SectionName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                return new LaunchHistoryDateGroupViewModel(
                    DateTime.SpecifyKind(g.Key, DateTimeKind.Unspecified),
                    launches.Count,
                    launches.Sum(i => i.Quantity),
                    launches.Sum(i => i.Hours),
                    launches,
                    sectionSummaries);
            })
            .ToList();

        var filter = new LaunchHistoryFilterViewModel
        {
            From = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified),
            To = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified),
        };

        var model = new LaunchHistoryViewModel(filter, grouped);

        return View("~/Views/Wip/LaunchHistory.cshtml", model);
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
                                  OperationNumber.Format(route.OpNumber),
                                  operation != null ? operation.Name : string.Empty,
                                  route.NormHours,
                                  route.SectionId,
                                  section != null ? section.Name : string.Empty,
                                  balance != null ? balance.Quantity : 0m);

        var operationItems = await operationsQuery
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(operationItems);
    }

    [HttpGet("tail")]
    public async Task<IActionResult> GetTail([FromQuery] Guid partId, [FromQuery] string? opNumber, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty || string.IsNullOrWhiteSpace(opNumber))
        {
            return BadRequest("Недостаточно данных для расчёта хвоста маршрута.");
        }

        var normalizedOpNumber = OperationNumber.Normalize(opNumber);

        if (!OperationNumber.TryParse(normalizedOpNumber, out _))
        {
            return BadRequest("Неверный номер операции.");
        }

        var tail = await _routeService.GetTailToFinishAsync(partId, normalizedOpNumber, cancellationToken)
            .ConfigureAwait(false);

        var operations = tail
            .Select(x => new LaunchTailOperationViewModel(
                OperationNumber.Format(x.OpNumber),
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

        List<LaunchItemDto> dtos;
        try
        {
            dtos = request.Items
                .Select(x => new LaunchItemDto(
                    x.PartId,
                    OperationNumber.Parse(x.FromOpNumber, nameof(LaunchSaveItem.FromOpNumber)),
                    x.LaunchDate,
                    x.Quantity,
                    x.Comment))
                .ToList();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var summary = await _launchService.AddLaunchesBatchAsync(dtos, cancellationToken).ConfigureAwait(false);

        var model = new LaunchBatchSummaryViewModel(
            summary.Saved,
            summary.Items
                .Select(x => new LaunchBatchItemViewModel(
                    x.PartId,
                    OperationNumber.Format(x.FromOpNumber),
                    x.SectionId,
                    x.Quantity,
                    x.Remaining,
                    x.SumHoursToFinish,
                    x.LaunchId))
                .ToList());

        return Ok(model);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор запуска.");
        }

        if (User?.Identity?.IsAuthenticated != true)
        {
            return Forbid();
        }

        try
        {
            var result = await _launchService.DeleteLaunchAsync(id, cancellationToken).ConfigureAwait(false);
            var message = $"Запуск успешно удалён. Текущий остаток: {result.Remaining:0.###}";
            var response = new LaunchDeleteResponseModel(
                result.LaunchId,
                result.PartId,
                result.SectionId,
                OperationNumber.Format(result.FromOpNumber),
                result.Remaining,
                message);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        var (fromDate, toDate) = NormalizePeriod(from, to);
        var fromUtc = ToUtcStartOfDay(fromDate);
        var toUtc = ToUtcStartOfDay(toDate.AddDays(1)).AddTicks(-1);

        var file = await _reportService.ExportLaunchesToExcelAsync(fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var fileName = $"Запуски_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("history/export-by-date")]
    public async Task<IActionResult> ExportByDate([FromQuery] DateTime date, CancellationToken cancellationToken)
    {
        if (date == default)
        {
            return BadRequest("Не указана дата для экспорта.");
        }

        var normalizedDate = date.Date;
        var file = await _reportService.ExportLaunchesByDateAsync(normalizedDate, cancellationToken).ConfigureAwait(false);
        var fileName = $"Запуски_{normalizedDate:yyyyMMdd}.xlsx";
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public record LaunchSaveRequest(IReadOnlyList<LaunchSaveItem> Items);

    public record LaunchSaveItem(
        Guid PartId,
        string FromOpNumber,
        DateTime LaunchDate,
        decimal Quantity,
        string? Comment);

    private static (DateTime From, DateTime To) NormalizePeriod(DateTime from, DateTime to)
    {
        var normalizedFrom = from.Date;
        var normalizedTo = to.Date;

        if (normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return (normalizedFrom, normalizedTo);
    }

    private static DateTime ToUtcStartOfDay(DateTime date)
    {
        var local = DateTime.SpecifyKind(date.Date, DateTimeKind.Local);
        return local.ToUniversalTime();
    }

    private static DateTime ToLocalDateTime(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        var local = utcValue.ToLocalTime();
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }
}
