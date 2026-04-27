using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Launches;
using UchetNZP.Application.Services;
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

        var requirementCandidates = await _dbContext.MetalRequirements
            .AsNoTracking()
            .Where(x => launchIds.Contains(x.WipLaunchId))
            .Select(x => new
            {
                x.WipLaunchId,
                RequirementId = x.Id,
                x.RequirementNumber,
                x.RequirementDate,
                x.Status,
                PlanIndicatorStatus = x.RequirementPlans
                    .OrderByDescending(p => p.RecalculatedAt ?? p.CreatedAt)
                    .Select(p => p.DeficitQty > 0m ? "Есть дефицит" : "План рассчитан")
                    .FirstOrDefault() ?? "План не рассчитан",
                x.MetalMaterialId,
                ItemMaterialId = x.Items
                    .OrderBy(i => i.Id)
                    .Select(i => i.MetalMaterialId)
                    .FirstOrDefault(),
                ItemFormula = x.Items
                    .OrderBy(i => i.Id)
                    .Select(i => i.CalculationFormula)
                    .FirstOrDefault()
            }).ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var requirementsByLaunch = requirementCandidates
            .GroupBy(x => x.WipLaunchId)
            .Select(g => g.OrderByDescending(x => x.RequirementDate).ThenByDescending(x => x.RequirementId).First())
            .ToDictionary(
                x => x.WipLaunchId,
                x => x);

        var normsByPart = norms.GroupBy(x => x.PartId).ToDictionary(x => x.Key, x => x.ToList());
        var activeMaterialLookup = await _dbContext.MetalMaterials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken)
            .ConfigureAwait(false);

        var analysisMaterialId = query?.MetalMaterialId is { } queryMaterialId && queryMaterialId != Guid.Empty
            ? queryMaterialId
            : (Guid?)null;

        var materialOptions = await _dbContext.MetalMaterials.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken).ConfigureAwait(false);


        var items = launches
            .Select(x =>
            {
                normsByPart.TryGetValue(x.PartId, out var launchNorms);
                requirementsByLaunch.TryGetValue(x.Id, out var requirementProjection);

                var requirementMaterialId = requirementProjection?.MetalMaterialId
                    ?? requirementProjection?.ItemMaterialId;
                var effectiveMaterialId = analysisMaterialId ?? requirementMaterialId;
                var selectedMaterial = effectiveMaterialId.HasValue &&
                    activeMaterialLookup.TryGetValue(effectiveMaterialId.Value, out var materialCandidate)
                        ? materialCandidate
                        : null;

                var needs = (launchNorms ?? [])
                    .Select(norm =>
                    {
                        if (selectedMaterial is null)
                        {
                            return null;
                        }

                        stockByMaterial.TryGetValue(selectedMaterial.Id, out var stock);
                        var calculation = MetalConsumptionCalculator.Calculate(norm, x.Quantity, selectedMaterial);
                        var weightPerUnitKg = ResolveWeightPerUnitKg(selectedMaterial, norm.ConsumptionUnit);
                        var formula = BuildFormulaDisplay(norm.BaseConsumptionQty, x.Quantity, norm.ConsumptionUnit, weightPerUnitKg, selectedMaterial, calculation.NeedKg);

                        return new LaunchMetalNeedItemViewModel(
                            selectedMaterial.Id,
                            selectedMaterial.Name,
                            selectedMaterial.Code,
                            norm.BaseConsumptionQty,
                            norm.SizeRaw,
                            norm.ConsumptionUnit,
                            x.Quantity,
                            calculation.NeedM,
                            calculation.NeedM2,
                            calculation.NeedPcs,
                            norm.ConsumptionUnit,
                            weightPerUnitKg,
                            ResolveCoefficient(selectedMaterial),
                            selectedMaterial.StockUnit,
                            calculation.NeedKg,
                            formula,
                            stock?.Qty ?? 0m,
                            stock?.WeightKg ?? 0m);
                    })
                    .Where(x => x is not null)
                    .Cast<LaunchMetalNeedItemViewModel>()
                    .ToList();

                var requirement = requirementProjection is null
                    ? null
                    : new LaunchMetalRequirementShortViewModel(
                        requirementProjection.RequirementId,
                        requirementProjection.RequirementNumber,
                        requirementProjection.RequirementDate,
                        requirementProjection.Status,
                        requirementProjection.PlanIndicatorStatus);
                var requirementStatusMessage = ResolveRequirementStatusMessage(
                    requirement,
                    requirementProjection?.Status,
                    requirementMaterialId,
                    analysisMaterialId,
                    launchNorms,
                    activeMaterialLookup);

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
                    requirement,
                    requirementStatusMessage);
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
            MetalMaterialId = analysisMaterialId,
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

    public record LaunchSaveRequest(IReadOnlyList<LaunchSaveItem> Items);

    public record LaunchSaveItem(
        Guid PartId,
        string FromOpNumber,
        DateTime LaunchDate,
        decimal Quantity,
        string? Comment);

    private static string? ResolveRequirementStatusMessage(
        LaunchMetalRequirementShortViewModel? requirement,
        string? requirementStatus,
        Guid? requirementMaterialId,
        Guid? analysisMaterialId,
        IReadOnlyCollection<MetalConsumptionNorm>? launchNorms,
        IReadOnlyDictionary<Guid, MetalMaterial> activeMaterialLookup)
    {
        if (requirement is not null &&
            !(string.Equals(requirementStatus, "Draft", StringComparison.OrdinalIgnoreCase) && !requirementMaterialId.HasValue))
        {
            return null;
        }

        if (string.Equals(requirementStatus, "Draft", StringComparison.OrdinalIgnoreCase) && !requirementMaterialId.HasValue)
        {
            return "Требование в статусе Draft: материал не выбран, расчёт по норме недоступен.";
        }

        if (analysisMaterialId.HasValue)
        {
            return "Материал выбран в режиме анализа (what-if).";
        }

        if (launchNorms is null || launchNorms.Count == 0)
        {
            return "Требование не сформировано: не найдена норма расхода";
        }

        var hasValidMaterial = launchNorms
            .Any(x => x.MetalMaterialId.HasValue && x.MetalMaterialId.Value != Guid.Empty && activeMaterialLookup.ContainsKey(x.MetalMaterialId.Value));

        return hasValidMaterial
            ? null
            : "Требование не сформировано: не выбран материал";
    }

    private static decimal ResolveCoefficient(MetalMaterial material)
    {
        if (material.Coefficient > 0m)
        {
            return material.Coefficient;
        }

        if (material.CoefConsumption > 0m)
        {
            return material.CoefConsumption;
        }

        return 1m;
    }

    private static decimal ResolveWeightPerUnitKg(MetalMaterial material, string? unit)
    {
        if (material.WeightPerUnitKg.HasValue && material.WeightPerUnitKg.Value > 0m)
        {
            return material.WeightPerUnitKg.Value;
        }

        var normalizedUnit = NormalizeUnit(unit);
        return normalizedUnit switch
        {
            "m" when material.MassPerMeterKg > 0m => material.MassPerMeterKg,
            "m2" when material.MassPerSquareMeterKg > 0m => material.MassPerSquareMeterKg,
            _ => 0m,
        };
    }

    private static string BuildFormulaDisplay(decimal baseConsumptionQty, decimal quantity, string? unit, decimal weightPerUnitKg, MetalMaterial material, decimal needKg)
    {
        var normalizedUnit = NormalizeUnit(unit);
        var coefficient = ResolveCoefficient(material);

        return normalizedUnit switch
        {
            "m" => $"NeedM = {quantity:0.###} × {baseConsumptionQty:0.###}; NeedKg = {quantity * baseConsumptionQty:0.###} × {weightPerUnitKg:0.###} × {coefficient:0.###} = {needKg:0.###}",
            "m2" => $"NeedM2 = {quantity:0.###} × {baseConsumptionQty:0.###}; NeedKg = {quantity * baseConsumptionQty:0.###} × {weightPerUnitKg:0.###} × {coefficient:0.###} = {needKg:0.###}",
            "g" => $"NeedKg = {quantity:0.###} × ({baseConsumptionQty:0.###} / 1000) × {coefficient:0.###} = {needKg:0.###}",
            "kg" => $"NeedKg = {quantity:0.###} × {baseConsumptionQty:0.###} × {coefficient:0.###} = {needKg:0.###}",
            _ => $"NeedQty = {quantity:0.###} × {baseConsumptionQty:0.###}; NeedKg = NeedQty × {coefficient:0.###} = {needKg:0.###}",
        };
    }

    private static string NormalizeUnit(string? unit)
    {
        var normalized = (unit ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "м" => "m",
            "м2" or "м²" => "m2",
            _ => normalized,
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

    private static MaterialSelectionResult ResolveMaterialSelection(
        WipLaunch launch,
        MetalConsumptionNorm norm,
        IReadOnlyCollection<PartToMaterialRule> rules,
        IReadOnlyCollection<MetalMaterial> activeMaterials,
        Guid? manualMaterialId)
    {
        if (manualMaterialId.HasValue && manualMaterialId.Value != Guid.Empty)
        {
            var manualMaterial = activeMaterials.FirstOrDefault(x => x.Id == manualMaterialId.Value);
            if (manualMaterial is not null)
            {
                return MaterialSelectionResult.Resolved(
                    manualMaterial,
                    "manual",
                    "Материал выбран пользователем вручную.",
                    []);
            }
        }

        var partName = launch.Part?.Name ?? string.Empty;
        var exactCandidates = rules
            .Where(rule => IsPatternMatch(partName, rule.PartNamePattern))
            .Where(rule => string.Equals(rule.GeometryType, norm.ShapeType, StringComparison.OrdinalIgnoreCase))
            .Where(rule => IsRuleSizeMatch(rule, norm))
            .OrderByDescending(rule => rule.Priority)
            .Select(rule =>
            {
                var material = activeMaterials.FirstOrDefault(m =>
                    string.Equals(m.Code, rule.MaterialArticle, StringComparison.OrdinalIgnoreCase)
                    || m.Name.Contains(rule.MaterialArticle, StringComparison.OrdinalIgnoreCase));
                return new { Rule = rule, Material = material };
            })
            .Where(x => x.Material is not null)
            .ToList();

        if (exactCandidates.Count > 0)
        {
            var winner = exactCandidates[0];
            var candidateTexts = exactCandidates
                .Take(3)
                .Select(x => $"{x.Material!.Name} ({x.Material.Code ?? "без артикула"}): правило #{x.Rule.Priority}")
                .ToList();
            return MaterialSelectionResult.Resolved(
                winner.Material!,
                "auto_rule",
                $"Подобрано по точному правилу: {winner.Rule.PartNamePattern}/{winner.Rule.GeometryType}, приоритет {winner.Rule.Priority}.",
                candidateTexts);
        }

        var rolledType = DetectRolledType(norm, partName);
        var fallbackCandidates = activeMaterials
            .Select(material => new
            {
                Material = material,
                Score = CalculateFallbackScore(material, rolledType, norm),
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Material.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (fallbackCandidates.Count == 0)
        {
            return MaterialSelectionResult.Unresolved("Не удалось автоматически подобрать материал. Выберите материал вручную.");
        }

        var topScore = fallbackCandidates[0].Score;
        var ambiguous = fallbackCandidates.Count > 1 && fallbackCandidates[1].Score == topScore;
        var fallbackTexts = fallbackCandidates
            .Take(3)
            .Select(x => $"{x.Material.Name} ({x.Material.Code ?? "без артикула"}): score {x.Score}")
            .ToList();

        if (ambiguous)
        {
            return MaterialSelectionResult.Unresolved($"Найдено несколько равнозначных кандидатов ({string.Join("; ", fallbackTexts)}). Укажите материал вручную.");
        }

        return MaterialSelectionResult.Resolved(
            fallbackCandidates[0].Material,
            "fallback",
            $"Выбор по fallback для типа проката '{rolledType}'.",
            fallbackTexts);
    }

    private static bool IsPatternMatch(string partName, string pattern)
    {
        var normalizedPattern = (pattern ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedPattern))
        {
            return true;
        }

        return partName.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuleSizeMatch(PartToMaterialRule rule, MetalConsumptionNorm norm)
    {
        var size = norm.DiameterMm ?? norm.ThicknessMm ?? norm.WidthMm;
        if (!size.HasValue)
        {
            return true;
        }

        var from = rule.SizeFromMm ?? decimal.MinValue;
        var to = rule.SizeToMm ?? decimal.MaxValue;
        return size.Value >= from && size.Value <= to;
    }

    private static string DetectRolledType(MetalConsumptionNorm norm, string partName)
    {
        if (string.Equals(norm.ShapeType, "rod", StringComparison.OrdinalIgnoreCase) || partName.Contains("штыр", StringComparison.OrdinalIgnoreCase))
        {
            return "rod";
        }

        if (string.Equals(norm.ShapeType, "sheet", StringComparison.OrdinalIgnoreCase) || partName.Contains("бирк", StringComparison.OrdinalIgnoreCase))
        {
            return "sheet";
        }

        return norm.ShapeType switch
        {
            "pipe" => "pipe",
            _ => "sheet",
        };
    }

    private static int CalculateFallbackScore(MetalMaterial material, string rolledType, MetalConsumptionNorm norm)
    {
        var score = 0;
        var haystack = $"{material.Name} {material.Code}".ToLowerInvariant();

        if (rolledType == "rod" && (haystack.Contains("круг") || haystack.Contains("прут")))
        {
            score += 10;
        }
        else if (rolledType == "sheet" && haystack.Contains("лист"))
        {
            score += 10;
        }
        else if (rolledType == "pipe" && haystack.Contains("труб"))
        {
            score += 10;
        }

        var target = norm.DiameterMm ?? norm.ThicknessMm ?? norm.WidthMm;
        if (target.HasValue)
        {
            var marker = target.Value.ToString("0.###").Replace(',', '.');
            if (haystack.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }
        }

        return score;
    }

    private sealed record MaterialSelectionResult(bool IsResolved, MetalMaterial? Material, string Source, string Reason, string? CandidatesDisplay)
    {
        public static MaterialSelectionResult Resolved(MetalMaterial material, string source, string reason, IReadOnlyCollection<string> candidates)
        {
            var candidateString = candidates.Count == 0 ? null : string.Join("; ", candidates);
            return new MaterialSelectionResult(true, material, source, reason, candidateString);
        }

        public static MaterialSelectionResult Unresolved(string reason)
        {
            return new MaterialSelectionResult(false, null, "manual", reason, null);
        }
    }
}
