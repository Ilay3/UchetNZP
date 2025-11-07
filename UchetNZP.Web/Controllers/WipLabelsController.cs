using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("wip/labels")]
public class WipLabelsController : Controller
{
    private readonly AppDbContext m_dbContext;
    private readonly IWipLabelService m_wipLabelService;

    public WipLabelsController(AppDbContext in_dbContext, IWipLabelService in_wipLabelService)
    {
        m_dbContext = in_dbContext ?? throw new ArgumentNullException(nameof(in_dbContext));
        m_wipLabelService = in_wipLabelService ?? throw new ArgumentNullException(nameof(in_wipLabelService));
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var model = new WipLabelsPageViewModel(DateTime.Today);
        var ret = View("~/Views/Wip/Labels.cshtml", model);
        return ret;
    }

    [HttpGet("parts")]
    public async Task<IActionResult> GetParts([FromQuery(Name = "search")] string? in_search, CancellationToken in_cancellationToken)
    {
        var query = m_dbContext.Parts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(in_search))
        {
            var term = in_search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                (x.Code != null && x.Code.ToLower().Contains(term)));
        }

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var ret = Ok(items);
        return ret;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetLabels(
        [FromQuery(Name = "from")] DateTime? in_from,
        [FromQuery(Name = "to")] DateTime? in_to,
        [FromQuery(Name = "partId")] Guid? in_partId,
        CancellationToken in_cancellationToken)
    {
        var filter = new WipLabelFilterDto(in_from, in_to, in_partId);
        var labels = await m_wipLabelService
            .GetLabelsAsync(filter, in_cancellationToken)
            .ConfigureAwait(false);

        var items = labels
            .Select(MapToViewModel)
            .ToList();

        var ret = Ok(items);
        return ret;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateLabel([FromBody] WipLabelCreateInputModel? in_request, CancellationToken in_cancellationToken)
    {
        IActionResult ret;

        if (in_request is null)
        {
            ret = BadRequest("Запрос не может быть пустым.");
        }
        else if (in_request.PartId == Guid.Empty)
        {
            ret = BadRequest("Не выбрана деталь.");
        }
        else if (in_request.Quantity <= 0)
        {
            ret = BadRequest("Количество должно быть больше нуля.");
        }
        else
        {
            var dto = new WipLabelCreateDto(in_request.PartId, in_request.LabelDate, in_request.Quantity);
            var created = await m_wipLabelService
                .CreateLabelAsync(dto, in_cancellationToken)
                .ConfigureAwait(false);

            var item = MapToViewModel(created);
            ret = Ok(item);
        }

        return ret;
    }

    [HttpPost("batch")]
    public async Task<IActionResult> CreateLabelsBatch([FromBody] WipLabelBatchInputModel? in_request, CancellationToken in_cancellationToken)
    {
        IActionResult ret;

        if (in_request is null)
        {
            ret = BadRequest("Запрос не может быть пустым.");
        }
        else if (in_request.PartId == Guid.Empty)
        {
            ret = BadRequest("Не выбрана деталь.");
        }
        else if (in_request.Quantity <= 0)
        {
            ret = BadRequest("Количество должно быть больше нуля.");
        }
        else if (in_request.Count <= 0)
        {
            ret = BadRequest("Количество ярлыков должно быть больше нуля.");
        }
        else
        {
            var dto = new WipLabelBatchCreateDto(in_request.PartId, in_request.LabelDate, in_request.Quantity, in_request.Count);
            var created = await m_wipLabelService
                .CreateLabelsBatchAsync(dto, in_cancellationToken)
                .ConfigureAwait(false);

            var items = created
                .Select(MapToViewModel)
                .ToList();

            ret = Ok(items);
        }

        return ret;
    }

    private static WipLabelListItemViewModel MapToViewModel(WipLabelDto in_label)
    {
        if (in_label is null)
        {
            throw new ArgumentNullException(nameof(in_label));
        }

        var ret = new WipLabelListItemViewModel(
            in_label.Id,
            in_label.PartId,
            in_label.PartName,
            in_label.PartCode,
            in_label.Number,
            in_label.LabelDate,
            in_label.Quantity,
            in_label.IsAssigned);

        return ret;
    }
}
