using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Launches;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Infrastructure;
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

        var launchIds = launches.Select(x => x.Id).ToList();
        var partIds = launches.Select(x => x.PartId).Distinct().ToList();

        var norms = await _dbContext.MetalConsumptionNorms
            .AsNoTracking()
            .Where(x => x.IsActive && partIds.Contains(x.PartId))
            .Include(x => x.MetalMaterial)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var stockByMaterial = await _dbContext.MetalReceiptItems
            .AsNoTracking()
            .GroupBy(x => x.MetalMaterialId)
            .Select(x => new
            {
                MetalMaterialId = x.Key,
                Qty = x.Sum(i => i.SizeValue),
                WeightKg = x.Sum(i => i.Quantity > 0 ? i.TotalWeightKg / i.Quantity : i.TotalWeightKg),
            })
            .ToDictionaryAsync(x => x.MetalMaterialId, cancellationToken)
            .ConfigureAwait(false);

        var requirementsByLaunch = await _dbContext.MetalRequirements
            .AsNoTracking()
            .Where(x => launchIds.Contains(x.WipLaunchId))
            .GroupBy(x => x.WipLaunchId)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
            .ToDictionaryAsync(
                x => x.WipLaunchId,
                x => new LaunchMetalRequirementShortViewModel(x.Id, x.RequirementNumber, x.RequirementDate, x.Status),
                cancellationToken)
            .ConfigureAwait(false);

        var normsByPart = norms.GroupBy(x => x.PartId).ToDictionary(x => x.Key, x => x.ToList());

        var selectedMaterialId = query?.MetalMaterialId;
        if (!selectedMaterialId.HasValue || selectedMaterialId == Guid.Empty)
        {
            selectedMaterialId = await _dbContext.MetalMaterials.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        MetalMaterial? selectedMaterial = null;
        if (selectedMaterialId.HasValue)
        {
            selectedMaterial = await _dbContext.MetalMaterials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == selectedMaterialId.Value, cancellationToken).ConfigureAwait(false);
        }

        var materialOptions = await _dbContext.MetalMaterials.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken).ConfigureAwait(false);


        var items = launches
            .Select(x =>
            {
                normsByPart.TryGetValue(x.PartId, out var launchNorms);
                var needs = (launchNorms ?? [])
                    .Select(norm =>
                    {
                        if (selectedMaterial is null)
                        {
                            return null;
                        }

                        stockByMaterial.TryGetValue(selectedMaterial.Id, out var stock);
                        var requiredQty = norm.BaseConsumptionQty * x.Quantity;
                        var totalWeightKg = CalculateTotalWeightKg(requiredQty, norm.ConsumptionUnit, selectedMaterial.WeightPerUnitKg, selectedMaterial.Coefficient);

                        return new LaunchMetalNeedItemViewModel(
                            selectedMaterial.Id,
                            selectedMaterial.Name,
                            selectedMaterial.Code,
                            norm.BaseConsumptionQty,
                            norm.SizeRaw,
                            x.Quantity,
                            requiredQty,
                            norm.ConsumptionUnit,
                            selectedMaterial.WeightPerUnitKg,
                            selectedMaterial.Coefficient,
                            totalWeightKg,
                            stock?.Qty ?? 0m,
                            stock?.WeightKg ?? 0m);
                    })
                    .Where(x => x is not null)
                    .Cast<LaunchMetalNeedItemViewModel>()
                    .ToList();

                requirementsByLaunch.TryGetValue(x.Id, out var requirement);

                return new LaunchHistoryItemViewModel(
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
                        .ToList(),
                    needs,
                    requirement);
            })
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
            MetalMaterialId = selectedMaterialId,
            MaterialOptions = materialOptions,
        };

        var model = new LaunchHistoryViewModel(filter, grouped);

        return View("~/Views/Wip/LaunchHistory.cshtml", model);
    }

    [HttpGet("parts")]
    public async Task<IActionResult> GetParts([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Parts.AsNoTracking();

        query = query.WhereMatchesLookup(search, x => x.Name, x => x.Code);

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
            dtos = CreateLaunchItemDtos(request.Items);
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

    [HttpPost("export-cart")]
    public async Task<IActionResult> ExportCart([FromBody] LaunchSaveRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("Список запусков пуст.");
        }

        List<LaunchItemDto> dtos;
        try
        {
            dtos = CreateLaunchItemDtos(request.Items);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        try
        {
            var file = await _reportService.ExportLaunchCartAsync(dtos, cancellationToken).ConfigureAwait(false);
            return File(
                file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Запуски_корзина.xlsx");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
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

    [HttpPost("{id:guid}/metal-requirements")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMetalRequirement(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор запуска.");
        }

        var launch = await _dbContext.WipLaunches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (launch is null)
        {
            return NotFound("Запуск не найден.");
        }

        var existing = await _dbContext.MetalRequirements
            .AsNoTracking()
            .Where(x => x.WipLaunchId == id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            TempData["LaunchMetalRequirementMessage"] = "Требование уже было создано для этого запуска.";
            return RedirectToAction(nameof(History));
        }

        var selectedMaterialId = await _dbContext.MetalMaterials.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!selectedMaterialId.HasValue)
        {
            TempData["LaunchMetalRequirementError"] = "Не найден активный материал.";
            return RedirectToAction(nameof(History));
        }

        var selectedMaterial = await _dbContext.MetalMaterials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == selectedMaterialId.Value, cancellationToken).ConfigureAwait(false);
        var norms = await _dbContext.MetalConsumptionNorms
            .AsNoTracking()
            .Where(x => x.IsActive && x.PartId == launch.PartId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (norms.Count == 0)
        {
            TempData["LaunchMetalRequirementError"] = "Норма расхода для детали и материала не найдена.";
            return RedirectToAction(nameof(History));
        }

        var requirement = new MetalRequirement
        {
            Id = Guid.NewGuid(),
            RequirementNumber = await GetNextRequirementNumberAsync(cancellationToken).ConfigureAwait(false),
            RequirementDate = DateTime.UtcNow,
            WipLaunchId = launch.Id,
            PartId = launch.PartId,
            Quantity = launch.Quantity,
            Status = "Создано",
            CreatedAt = DateTime.UtcNow,
            Comment = "Черновик создан автоматически из запуска партии.",
            Items = norms.Select(norm => new MetalRequirementItem
            {
                Id = Guid.NewGuid(),
                MetalMaterialId = selectedMaterialId.Value,
                NormPerUnit = norm.BaseConsumptionQty,
                TotalRequiredQty = norm.BaseConsumptionQty * launch.Quantity,
                Unit = norm.ConsumptionUnit,
                TotalRequiredWeightKg = CalculateTotalWeightKg(norm.BaseConsumptionQty * launch.Quantity, norm.ConsumptionUnit, selectedMaterial?.WeightPerUnitKg, selectedMaterial?.Coefficient ?? 1m),
            }).ToList(),
        };

        _dbContext.MetalRequirements.Add(requirement);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        TempData["LaunchMetalRequirementMessage"] = $"Создано требование {requirement.RequirementNumber}.";

        return RedirectToAction(nameof(History));
    }

    public record LaunchSaveRequest(IReadOnlyList<LaunchSaveItem> Items);

    public record LaunchSaveItem(
        Guid PartId,
        string FromOpNumber,
        DateTime LaunchDate,
        decimal Quantity,
        string? Comment);

    private async Task<string> GetNextRequirementNumberAsync(CancellationToken cancellationToken)
    {
        var lastNumber = await _dbContext.MetalRequirements
            .AsNoTracking()
            .Where(x => x.RequirementNumber.StartsWith("MREQ-"))
            .OrderByDescending(x => x.RequirementNumber)
            .Select(x => x.RequirementNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var numericPart = 0;
        if (!string.IsNullOrWhiteSpace(lastNumber) && lastNumber.Length > 5)
        {
            _ = int.TryParse(lastNumber[5..], out numericPart);
        }

        return $"MREQ-{(numericPart + 1):D6}";
    }


    private static decimal CalculateTotalWeightKg(decimal requiredQty, string unit, decimal? weightPerUnitKg, decimal coefficient)
    {
        var normalizedUnit = (unit ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedUnit switch
        {
            "м" or "m" or "м2" or "m2" or "м²" => requiredQty * (weightPerUnitKg ?? 0m) * coefficient,
            "кг" or "kg" => requiredQty * coefficient,
            "г" or "g" => (requiredQty / 1000m) * coefficient,
            _ => requiredQty * coefficient,
        };
    }

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

    private static List<LaunchItemDto> CreateLaunchItemDtos(IReadOnlyList<LaunchSaveItem> items)
    {
        var ret = new List<LaunchItemDto>(items.Count);

        foreach (var item in items)
        {
            var dto = new LaunchItemDto(
                item.PartId,
                OperationNumber.Parse(item.FromOpNumber, nameof(LaunchSaveItem.FromOpNumber)),
                item.LaunchDate,
                item.Quantity,
                item.Comment);
            ret.Add(dto);
        }

        return ret;
    }
}
