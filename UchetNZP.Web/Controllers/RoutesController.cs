using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;
using UchetNZP.Shared;

namespace UchetNZP.Web.Controllers;

[Route("routes")]
public class RoutesController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IRouteService _routeService;
    private readonly IImportService _importService;
    private readonly IReportService _reportService;

    public RoutesController(AppDbContext dbContext, IRouteService routeService, IImportService importService, IReportService reportService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] RouteListQuery? query, CancellationToken cancellationToken)
    {
        query ??= new RouteListQuery();

        var sections = await _dbContext.Sections
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var routesQuery = from route in _dbContext.PartRoutes.AsNoTracking()
                          join part in _dbContext.Parts.AsNoTracking() on route.PartId equals part.Id
                          join operation in _dbContext.Operations.AsNoTracking() on route.OperationId equals operation.Id into operations
                          from operation in operations.DefaultIfEmpty()
                          join section in _dbContext.Sections.AsNoTracking() on route.SectionId equals section.Id into sectionsJoin
                          from section in sectionsJoin.DefaultIfEmpty()
                          select new { route, part, operation, section };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            routesQuery = routesQuery.Where(x =>
                x.part.Name.ToLower().Contains(term) ||
                (x.part.Code != null && x.part.Code.ToLower().Contains(term)) ||
                (x.operation != null && x.operation.Name.ToLower().Contains(term)) ||
                (x.section != null && x.section.Name.ToLower().Contains(term)));
        }

        if (query.SectionId.HasValue && query.SectionId.Value != Guid.Empty)
        {
            var sectionId = query.SectionId.Value;
            routesQuery = routesQuery.Where(x => x.section != null && x.section.Id == sectionId);
        }

        var routes = await routesQuery
            .OrderBy(x => x.part.Name)
            .ThenBy(x => x.route.OpNumber)
            .Select(x => new RouteListItemViewModel(
                x.route.Id,
                x.part.Id,
                x.part.Name,
                x.part.Code,
                OperationNumber.Format(x.route.OpNumber),
                x.operation != null ? x.operation.Name : string.Empty,
                x.section != null ? x.section.Id : Guid.Empty,
                x.section != null ? x.section.Name : string.Empty,
                x.route.NormHours))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var filter = new RouteListFilterViewModel
        {
            Search = query.Search,
            SectionId = query.SectionId.HasValue && query.SectionId.Value != Guid.Empty ? query.SectionId : null,
            Sections = sections,
        };

        var model = new RouteListViewModel(filter, routes);

        return View("~/Views/Routes/List.cshtml", model);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] RouteListQuery? query, CancellationToken cancellationToken)
    {
        query ??= new RouteListQuery();

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search;
        Guid? sectionId = null;

        if (query.SectionId.HasValue && query.SectionId.Value != Guid.Empty)
        {
            sectionId = query.SectionId.Value;
        }

        var file = await _reportService.ExportRoutesToExcelAsync(search, sectionId, cancellationToken).ConfigureAwait(false);
        var fileName = $"Маршруты_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View("~/Views/Routes/Edit.cshtml", new RouteEditInputModel());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RouteEditInputModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Routes/Edit.cshtml", model);
        }

        var opNumber = model.GetOpNumberValue();

        await _routeService.UpsertRouteAsync(
            model.PartName,
            null,
            model.OperationName,
            opNumber,
            model.NormHours,
            model.SectionName,
            cancellationToken).ConfigureAwait(false);

        TempData["RouteMessage"] = "Маршрут успешно добавлен.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var route = await _dbContext.PartRoutes
            .AsNoTracking()
            .Include(x => x.Part)
            .Include(x => x.Operation)
            .Include(x => x.Section)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (route is null)
        {
            return NotFound();
        }

        var model = new RouteEditInputModel
        {
            Id = route.Id,
            PartName = route.Part != null ? route.Part.Name : string.Empty,
            OperationName = route.Operation != null ? route.Operation.Name : string.Empty,
            OpNumber = OperationNumber.Format(route.OpNumber),
            NormHours = route.NormHours,
            SectionName = route.Section != null ? route.Section.Name : string.Empty,
        };

        return View("~/Views/Routes/Edit.cshtml", model);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RouteEditInputModel model, CancellationToken cancellationToken)
    {
        if (model.Id != id)
        {
            ModelState.AddModelError(string.Empty, "Некорректный идентификатор маршрута.");
        }

        if (!ModelState.IsValid)
        {
            return View("~/Views/Routes/Edit.cshtml", model);
        }

        var opNumber = model.GetOpNumberValue();

        var existingRoute = await _dbContext.PartRoutes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (existingRoute is null)
        {
            return NotFound();
        }

        var updatedRoute = await _routeService.UpsertRouteAsync(
            model.PartName,
            null,
            model.OperationName,
            opNumber,
            model.NormHours,
            model.SectionName,
            cancellationToken).ConfigureAwait(false);

        if (updatedRoute.Id != existingRoute.Id)
        {
            _dbContext.PartRoutes.Remove(existingRoute);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        TempData["RouteMessage"] = "Маршрут успешно обновлён.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var route = await _dbContext.PartRoutes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (route is null)
        {
            TempData["RouteError"] = "Маршрут не найден.";
            return RedirectToAction(nameof(Index));
        }

        var hasLaunches = await _dbContext.WipLaunchOperations
            .AsNoTracking()
            .AnyAsync(x => x.PartRouteId == id, cancellationToken)
            .ConfigureAwait(false);

        if (hasLaunches)
        {
            TempData["RouteError"] = "Нельзя удалить маршрут, который используется в запусках.";
            return RedirectToAction(nameof(Index));
        }

        _dbContext.PartRoutes.Remove(route);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        TempData["RouteMessage"] = "Маршрут удалён.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("import")]
    public IActionResult Import()
    {
        return View("~/Views/Routes/Import.cshtml");
    }

    [HttpGet("import/parts")]
    public async Task<IActionResult> SearchParts([FromQuery] string? search, CancellationToken cancellationToken)
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

    [HttpGet("import/operations")]
    public async Task<IActionResult> SearchOperations([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Operations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                (x.Code != null && x.Code.ToLower().Contains(term)));
        }

        var operations = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Code,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (operations.Count == 0)
        {
            return Ok(Array.Empty<OperationLookupItemViewModel>());
        }

        var operationIds = operations.Select(x => x.Id).ToArray();

        var sectionPairs = await _dbContext.PartRoutes
            .AsNoTracking()
            .Where(route => operationIds.Contains(route.OperationId))
            .Join(
                _dbContext.Sections.AsNoTracking(),
                route => route.SectionId,
                section => section.Id,
                (route, section) => new { route.OperationId, section.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sectionsByOperation = sectionPairs
            .GroupBy(x => x.OperationId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(x => x.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var items = operations
            .Select(operation => new OperationLookupItemViewModel(
                operation.Id,
                operation.Name,
                operation.Code,
                sectionsByOperation.TryGetValue(operation.Id, out var sections)
                    ? sections
                    : Array.Empty<string>()))
            .ToList();

        return Ok(items);
    }

    [HttpGet("import/sections")]
    public async Task<IActionResult> SearchSections([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Sections.AsNoTracking();

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

    [HttpPost("import/upsert")]
    public async Task<IActionResult> Upsert([FromBody] RouteUpsertRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Данные маршрута не заполнены.");
        }

        if (string.IsNullOrWhiteSpace(request.PartName))
        {
            return BadRequest("Наименование детали обязательно.");
        }

        if (request.Operations is null || request.Operations.Count == 0)
        {
            return BadRequest("Добавьте хотя бы одну операцию.");
        }

        var operations = new List<(string? OperationName, int OpNumber, decimal NormHours, string SectionName)>();

        foreach (var operation in request.Operations)
        {
            if (operation is null)
            {
                return BadRequest("Данные операции заполнены не полностью.");
            }

            if (operation.NormHours <= 0)
            {
                return BadRequest("Норматив должен быть больше нуля.");
            }

            if (string.IsNullOrWhiteSpace(operation.SectionName))
            {
                return BadRequest("Укажите вид работ для каждой операции.");
            }

            var normalizedOperationName = NormalizeName(operation.OperationName);
            var normalizedSectionName = NormalizeName(operation.SectionName);

            if (!string.IsNullOrEmpty(normalizedOperationName) &&
                !string.IsNullOrEmpty(normalizedSectionName) &&
                !string.Equals(normalizedOperationName, normalizedSectionName, StringComparison.Ordinal))
            {
                return BadRequest("Наименование операции и выбранный вид работ должны совпадать.");
            }

            int opNumber;
            try
            {
                opNumber = OperationNumber.Parse(operation.OpNumber, nameof(operation.OpNumber));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }

            operations.Add((operation.OperationName, opNumber, operation.NormHours, operation.SectionName));
        }

        foreach (var (operationName, opNumber, normHours, sectionName) in operations)
        {
            await _routeService.UpsertRouteAsync(
                request.PartName,
                request.PartCode,
                operationName,
                opNumber,
                normHours,
                sectionName,
                cancellationToken).ConfigureAwait(false);
        }

        return Ok(new { saved = operations.Count });
    }

    [HttpPost("import/upload")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadExcel(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Файл Excel не выбран.");
        }

        await using var stream = file.OpenReadStream();
        var summary = await _importService.ImportRoutesExcelAsync(stream, file.FileName, cancellationToken).ConfigureAwait(false);

        return Ok(summary);
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        return normalized.ToLowerInvariant();
    }

    public class RouteListQuery
    {
        public string? Search { get; set; }

        public Guid? SectionId { get; set; }
    }

    public record RouteUpsertRequest(
        string PartName,
        string? PartCode,
        IReadOnlyList<RouteUpsertOperation> Operations);

    public record RouteUpsertOperation(
        string? OperationName,
        string OpNumber,
        decimal NormHours,
        string SectionName);
}
