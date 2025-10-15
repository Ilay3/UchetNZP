using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("routes")]
public class RoutesController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IRouteService _routeService;
    private readonly IImportService _importService;

    public RoutesController(AppDbContext dbContext, IRouteService routeService, IImportService importService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
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

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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

        await _routeService.UpsertRouteAsync(
            request.PartName,
            request.PartCode,
            request.OperationName,
            request.OpNumber,
            request.NormHours,
            request.SectionName,
            cancellationToken).ConfigureAwait(false);

        return Ok();
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
        var summary = await _importService.ImportRoutesExcelAsync(stream, cancellationToken).ConfigureAwait(false);

        return Ok(summary);
    }

    public record RouteUpsertRequest(
        string PartName,
        string? PartCode,
        string OperationName,
        int OpNumber,
        decimal NormHours,
        string SectionName);
}
