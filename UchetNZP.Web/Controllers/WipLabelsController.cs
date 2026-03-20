using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Wip;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Web.Infrastructure;
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
        return RedirectToAction("Index", "WipReceipts");
    }

    [HttpGet("parts")]
    public async Task<IActionResult> GetParts([FromQuery(Name = "search")] string? in_search, CancellationToken in_cancellationToken)
    {
        var query = m_dbContext.Parts
            .AsNoTracking()
            .WhereMatchesLookup(in_search, x => x.Name, x => x.Code);

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, x.Code))
            .ToListAsync(in_cancellationToken)
            .ConfigureAwait(false);

        var ret = Ok(items);
        return ret;
    }

    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    [HttpGet("list")]
    public async Task<IActionResult> GetLabels(
        [FromQuery(Name = "from")] DateTime? in_from,
        [FromQuery(Name = "to")] DateTime? in_to,
        [FromQuery(Name = "partId")] Guid? in_partId,
        [FromQuery(Name = "page")] int? in_page,
        [FromQuery(Name = "pageSize")] int? in_pageSize,
        CancellationToken in_cancellationToken)
    {
        var filter = new WipLabelFilterDto(in_from, in_to, in_partId);
        var labels = await m_wipLabelService
            .GetLabelsAsync(filter, in_cancellationToken)
            .ConfigureAwait(false);

        var page = in_page.GetValueOrDefault(1);
        if (page < 1)
        {
            page = 1;
        }

        var pageSize = in_pageSize.GetValueOrDefault(DefaultPageSize);
        if (pageSize < 1)
        {
            pageSize = DefaultPageSize;
        }
        else if (pageSize > MaxPageSize)
        {
            pageSize = MaxPageSize;
        }

        var totalCount = labels.Count;
        var totalPages = totalCount > 0
            ? (int)Math.Ceiling(totalCount / (double)pageSize)
            : 0;

        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        if (totalPages == 0)
        {
            page = 1;
        }

        var skip = (page - 1) * pageSize;
        if (skip < 0)
        {
            skip = 0;
        }

        var items = labels
            .Skip(skip)
            .Take(pageSize)
            .Select(MapToViewModel)
            .ToList();

        var response = new WipLabelListResponseModel(items, page, totalPages, totalCount);

        var ret = Ok(response);
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

    [HttpPost("manual")]
    public async Task<IActionResult> CreateLabelManual([FromBody] WipLabelManualCreateInputModel? in_request, CancellationToken in_cancellationToken)
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
        else if (string.IsNullOrWhiteSpace(in_request.Number))
        {
            ret = BadRequest("Номер ярлыка не может быть пустым.");
        }
        else
        {
            try
            {
                var dto = new WipLabelManualCreateDto(in_request.PartId, in_request.LabelDate, in_request.Quantity, in_request.Number);
                var created = await m_wipLabelService
                    .CreateLabelWithNumberAsync(dto, in_cancellationToken)
                    .ConfigureAwait(false);

                var item = MapToViewModel(created);
                ret = Ok(item);
            }
            catch (InvalidOperationException ex)
            {
                ret = BadRequest(ex.Message);
            }
        }

        return ret;
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLabel(Guid id, [FromBody] WipLabelUpdateInputModel? in_request, CancellationToken in_cancellationToken)
    {
        IActionResult ret;

        if (in_request is null)
        {
            ret = BadRequest("Запрос не может быть пустым.");
        }
        else if (id == Guid.Empty || in_request.Id == Guid.Empty || id != in_request.Id)
        {
            ret = BadRequest("Некорректный идентификатор ярлыка.");
        }
        else if (in_request.Quantity <= 0)
        {
            ret = BadRequest("Количество должно быть больше нуля.");
        }
        else if (string.IsNullOrWhiteSpace(in_request.Number))
        {
            ret = BadRequest("Номер ярлыка не может быть пустым.");
        }
        else
        {
            try
            {
                var dto = new WipLabelUpdateDto(in_request.Id, in_request.LabelDate, in_request.Quantity, in_request.Number);
                var updated = await m_wipLabelService
                    .UpdateLabelAsync(dto, in_cancellationToken)
                    .ConfigureAwait(false);

                var item = MapToViewModel(updated);
                ret = Ok(item);
            }
            catch (InvalidOperationException ex)
            {
                ret = BadRequest(ex.Message);
            }
        }

        return ret;
    }

    [HttpGet("{id:guid}/state")]
    public async Task<IActionResult> GetLabelState(Guid id, CancellationToken in_cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор ярлыка.");
        }

        try
        {
            var state = await m_wipLabelService
                .GetLabelStateAsync(id, in_cancellationToken)
                .ConfigureAwait(false);

            return Ok(new WipLabelStateViewModel(
                state.Id,
                state.Number,
                state.Status,
                state.CurrentSectionId,
                state.CurrentOpNumber,
                state.RootLabelId,
                state.ParentLabelId,
                state.RootNumber,
                state.Suffix,
                state.Quantity,
                state.RemainingQuantity));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> GetLabelLedger(Guid id, CancellationToken in_cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор ярлыка.");
        }

        try
        {
            var events = await m_wipLabelService
                .GetLabelLedgerAsync(id, in_cancellationToken)
                .ConfigureAwait(false);

            var ret = events
                .Select(x => new WipLabelLedgerEventViewModel(
                    x.EventId,
                    x.EventTime,
                    x.UserId,
                    x.TransactionId,
                    x.EventType,
                    x.FromLabelId,
                    x.ToLabelId,
                    x.FromSectionId,
                    x.FromOpNumber,
                    x.ToSectionId,
                    x.ToOpNumber,
                    x.Qty,
                    x.ScrapQty,
                    x.RefEntityType,
                    x.RefEntityId))
                .ToList();

            return Ok(ret);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLabel(Guid id, CancellationToken in_cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор ярлыка.");
        }

        try
        {
            await m_wipLabelService
                .DeleteLabelAsync(id, in_cancellationToken)
                .ConfigureAwait(false);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("merge")]
    public async Task<IActionResult> MergeLabels([FromBody] WipLabelMergeInputModel? in_request, CancellationToken in_cancellationToken)
    {
        if (in_request is null)
        {
            return BadRequest("Запрос не может быть пустым.");
        }

        try
        {
            var result = await m_wipLabelService
                .MergeLabelsAsync(new WipLabelMergeRequestDto(in_request.InputLabelIds, in_request.MergeDate, in_request.NumberBase), in_cancellationToken)
                .ConfigureAwait(false);

            return Ok(new WipLabelMergeResultViewModel(result.OutputLabelId, result.OutputLabelNumber, result.Quantity, result.InputLabelIds));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict($"Конфликт параллельного изменения: {ex.Message}");
        }
        catch (DbUpdateException ex)
        {
            return BadRequest($"Операция merge отклонена ограничениями БД: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    [HttpGet("{id:guid}/merge-trace")]
    public async Task<IActionResult> GetMergeTrace(Guid id, CancellationToken in_cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest("Некорректный идентификатор ярлыка.");
        }

        try
        {
            var trace = await m_wipLabelService.GetMergeTraceAsync(id, in_cancellationToken).ConfigureAwait(false);
            return Ok(new WipLabelMergeTraceViewModel(
                trace.LabelId,
                trace.LabelNumber,
                trace.FromLabels.Select(x => new WipLabelMergeLinkViewModel(x.InputLabelId, x.InputLabelNumber, x.OutputLabelId, x.OutputLabelNumber, x.CreatedAt)).ToList(),
                trace.ToLabels.Select(x => new WipLabelMergeLinkViewModel(x.InputLabelId, x.InputLabelNumber, x.OutputLabelId, x.OutputLabelNumber, x.CreatedAt)).ToList()));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
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
