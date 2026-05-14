using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;
using UchetNZP.Web.Infrastructure;
using UchetNZP.Web.Models;
using UchetNZP.Web.Services;

namespace UchetNZP.Web.Controllers;

[Route("warehouse")]
public class WarehouseController : Controller
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const string MovementFilterAll = "all";
    private const string MovementFilterReceipts = "receipts";
    private const string MovementFilterIssues = "issues";
    private const string DefaultIssueRecipientName = "Сборщик СИП отдел";

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWarehouseControlCardDocumentService _controlCardDocumentService;

    public WarehouseController(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        IWarehouseControlCardDocumentService controlCardDocumentService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _controlCardDocumentService = controlCardDocumentService ?? throw new ArgumentNullException(nameof(controlCardDocumentService));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid? partId,
        string? partSearch = null,
        string? movement = null,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var statusMessage = TempData["WarehouseMessage"] as string;
        var errorMessage = TempData["WarehouseError"] as string;
        var printItemIdText = TempData["WarehousePrintItemId"] as string;
        var autoPrintUrl = Guid.TryParse(printItemIdText, out var printItemId)
            ? Url.Action(nameof(ControlCard), new { id = printItemId })
            : null;

        var model = await BuildIndexViewModelAsync(partId, partSearch, movement, statusMessage, errorMessage, autoPrintUrl, page, pageSize, cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    [HttpGet("{id:guid}/control-card")]
    public async Task<IActionResult> ControlCard(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _controlCardDocumentService.BuildAsync(id, cancellationToken).ConfigureAwait(false);
            return File(document.Content, document.ContentType, document.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("parts")]
    public async Task<IActionResult> GetParts([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var partQuery = _dbContext.Parts.AsNoTracking();

        partQuery = partQuery.WhereMatchesLookup(search, part => part.Name, part => part.Code);

        var items = await partQuery
            .OrderBy(part => part.Name)
            .Take(25)
            .Select(part => new LookupItemViewModel(part.Id, part.Name, part.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpGet("assembly-units")]
    public async Task<IActionResult> GetAssemblyUnits([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.WarehouseAssemblyUnits.AsNoTracking();

        query = query.WhereMatchesLookup(search, x => x.Name);

        var items = await query
            .OrderBy(x => x.Name)
            .Take(25)
            .Select(x => new LookupItemViewModel(x.Id, x.Name, null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items);
    }

    [HttpGet("labels")]
    public async Task<IActionResult> GetLabels([FromQuery] Guid partId, [FromQuery] string? search, [FromQuery] string? mode, CancellationToken cancellationToken)
    {
        if (partId == Guid.Empty)
        {
            return Ok(Array.Empty<WarehouseLabelLookupItemViewModel>());
        }

        var normalizedMode = string.Equals(mode, "issue", StringComparison.OrdinalIgnoreCase) ? "issue" : "receipt";
        var normalizedSearch = NormalizeSearchText(search);

        var labels = _dbContext.WipLabels
            .AsNoTracking()
            .Where(x => x.PartId == partId && x.Status == WipLabelStatus.Active);

        if (normalizedMode == "issue")
        {
            labels = labels
                .Where(x => (x.WarehouseLabelItems.Sum(item => (decimal?)item.Quantity) ?? 0m) > 0m);
        }
        else
        {
            labels = labels.Where(x => !x.IsAssigned);
        }

        foreach (var term in NormalizeSearchTerms(normalizedSearch))
        {
            labels = labels.Where(x => x.Number.ToLower().Contains(term.ToLower()));
        }

        var items = await labels
            .OrderBy(x => x.Number)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Quantity,
                AvailableQuantity = normalizedMode == "issue"
                    ? x.WarehouseLabelItems.Sum(item => (decimal?)item.Quantity) ?? 0m
                    : x.Quantity,
            })
            .Take(25)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items.Select(x => new WarehouseLabelLookupItemViewModel(
            x.Id,
            x.Number,
            x.Quantity,
            x.AvailableQuantity)));
    }

    [HttpGet("assembly-labels")]
    public async Task<IActionResult> GetAssemblyLabels([FromQuery] Guid assemblyUnitId, [FromQuery] string? search, [FromQuery] string? mode, CancellationToken cancellationToken)
    {
        if (assemblyUnitId == Guid.Empty)
        {
            return Ok(Array.Empty<WarehouseAssemblyLabelLookupItemViewModel>());
        }

        var normalizedMode = string.Equals(mode, "issue", StringComparison.OrdinalIgnoreCase) ? "issue" : "receipt";
        var normalizedSearch = NormalizeSearchText(search);

        var query = _dbContext.WarehouseLabelItems
            .AsNoTracking()
            .Where(x =>
                x.WarehouseItem != null &&
                x.WarehouseItem.AssemblyUnitId == assemblyUnitId &&
                x.LabelNumber != null &&
                x.LabelNumber != string.Empty);

        foreach (var term in NormalizeSearchTerms(normalizedSearch))
        {
            var searchTerm = term.ToLower();
            query = query.Where(x => x.LabelNumber != null && x.LabelNumber.ToLower().Contains(searchTerm));
        }

        var items = await query
            .GroupBy(x => x.LabelNumber!)
            .Select(group => new
            {
                Number = group.Key,
                Quantity = group.Where(x => x.Quantity > 0m).Sum(x => (decimal?)x.Quantity) ?? 0m,
                AvailableQuantity = group.Sum(x => (decimal?)x.Quantity) ?? 0m,
            })
            .Where(x => normalizedMode != "issue" || x.AvailableQuantity > 0m)
            .OrderBy(x => x.Number)
            .Take(25)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(items.Select(x => new WarehouseAssemblyLabelLookupItemViewModel(
            x.Number,
            x.Quantity,
            x.AvailableQuantity)));
    }

    [HttpPost("manual-receipt")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualReceipt(WarehouseManualReceiptModel model, CancellationToken cancellationToken)
    {
        if (model.PartId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.PartId), "Выберите деталь для ручного прихода.");
        }

        if (!ModelState.IsValid)
        {
            TempData["WarehouseError"] = "Проверьте данные ручного прихода.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var partExists = await _dbContext.Parts
                .AsNoTracking()
                .AnyAsync(x => x.Id == model.PartId, cancellationToken)
                .ConfigureAwait(false);

            if (!partExists)
            {
                throw new KeyNotFoundException("Деталь для ручного прихода не найдена.");
            }

            var now = DateTime.UtcNow;
            var itemId = Guid.NewGuid();
            var addedAt = NormalizeToUtc(model.ReceiptDate == default ? DateTime.Today : model.ReceiptDate);
            var documentNumber = TrimOrNull(model.DocumentNumber) ?? BuildManualWarehouseDocumentNumber(addedAt, itemId);
            var label = await ResolveReceiptLabelAsync(model.PartId, model.WipLabelId, model.LabelNumber, model.Quantity, addedAt, cancellationToken)
                .ConfigureAwait(false);
            var controlCardNumber = TrimOrNull(model.ControlCardNumber) ?? label?.Number ?? documentNumber;
            var userId = _currentUserService.UserId;

            var item = new WarehouseItem
            {
                Id = itemId,
                PartId = model.PartId,
                MovementType = WarehouseMovementKind.Receipt,
                SourceType = WarehouseMovementKind.ManualReceipt,
                DocumentNumber = documentNumber,
                ControlCardNumber = controlCardNumber,
                ControllerName = TrimOrNull(model.ControllerName),
                MasterName = TrimOrNull(model.MasterName),
                AcceptedByName = TrimOrNull(model.AcceptedByName),
                CreatedByUserId = userId == Guid.Empty ? null : userId,
                Quantity = model.Quantity,
                AddedAt = addedAt,
                CreatedAt = now,
                UpdatedAt = now,
                Comment = TrimOrNull(model.Comment),
            };

            await _dbContext.WarehouseItems.AddAsync(item, cancellationToken).ConfigureAwait(false);
            if (label is not null)
            {
                await AddWarehouseLabelItemAsync(item, label, model.Quantity, addedAt, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            TempData["WarehouseMessage"] = $"Ручной приход {documentNumber} создан.";
            if (model.PrintControlCard)
            {
                TempData["WarehousePrintItemId"] = item.Id.ToString();
            }

            return RedirectToAction(nameof(Index), new { partId = model.PartId });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["WarehouseError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("manual-assembly-unit-receipt")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualAssemblyUnitReceipt(WarehouseAssemblyUnitReceiptModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.AssemblyUnitName))
        {
            ModelState.AddModelError(nameof(model.AssemblyUnitName), "Укажите сборочный узел для ручного прихода.");
        }

        if (!ModelState.IsValid)
        {
            TempData["WarehouseError"] = "Проверьте данные прихода сборочного узла.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var assemblyUnit = await ResolveAssemblyUnitAsync(
                    model.AssemblyUnitId,
                    model.AssemblyUnitName,
                    createIfMissing: true,
                    cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var itemId = Guid.NewGuid();
            var addedAt = NormalizeToUtc(model.ReceiptDate == default ? DateTime.Today : model.ReceiptDate);
            var documentNumber = TrimOrNull(model.DocumentNumber) ?? BuildManualWarehouseDocumentNumber(addedAt, itemId);
            var labelNumber = await ResolveAssemblyReceiptLabelNumberAsync(assemblyUnit.Id, model.LabelNumber, cancellationToken)
                .ConfigureAwait(false);
            var controlCardNumber = TrimOrNull(model.ControlCardNumber) ?? labelNumber ?? documentNumber;
            var userId = _currentUserService.UserId;

            var item = new WarehouseItem
            {
                Id = itemId,
                AssemblyUnitId = assemblyUnit.Id,
                MovementType = WarehouseMovementKind.Receipt,
                SourceType = WarehouseMovementKind.ManualReceipt,
                DocumentNumber = documentNumber,
                ControlCardNumber = controlCardNumber,
                ControllerName = TrimOrNull(model.ControllerName),
                MasterName = TrimOrNull(model.MasterName),
                AcceptedByName = TrimOrNull(model.AcceptedByName),
                CreatedByUserId = userId == Guid.Empty ? null : userId,
                Quantity = model.Quantity,
                AddedAt = addedAt,
                CreatedAt = now,
                UpdatedAt = now,
                Comment = TrimOrNull(model.Comment),
            };

            await _dbContext.WarehouseItems.AddAsync(item, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(labelNumber))
            {
                await AddWarehouseLabelItemAsync(item, labelNumber, model.Quantity, addedAt, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            TempData["WarehouseMessage"] = $"Приход сборочного узла {documentNumber} создан.";
            if (model.PrintControlCard)
            {
                TempData["WarehousePrintItemId"] = item.Id.ToString();
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["WarehouseError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("manual-issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualIssue(WarehouseManualIssueModel model, CancellationToken cancellationToken)
    {
        if (model.PartId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.PartId), "Выберите деталь для ручного расхода.");
        }

        if (!ModelState.IsValid)
        {
            TempData["WarehouseError"] = "Проверьте данные ручного расхода детали.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var partExists = await _dbContext.Parts
                .AsNoTracking()
                .AnyAsync(x => x.Id == model.PartId, cancellationToken)
                .ConfigureAwait(false);

            if (!partExists)
            {
                throw new KeyNotFoundException("Деталь для ручного расхода не найдена.");
            }

            await EnsureAvailableBalanceAsync(model.PartId, null, model.Quantity, cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var itemId = Guid.NewGuid();
            var issueDate = NormalizeToUtc(model.IssueDate == default ? DateTime.Today : model.IssueDate);
            var documentNumber = TrimOrNull(model.DocumentNumber) ?? BuildManualWarehouseDocumentNumber(issueDate, itemId, "ISS");
            var label = await ResolveIssueLabelAsync(model.PartId, model.WipLabelId, model.LabelNumber, model.Quantity, cancellationToken)
                .ConfigureAwait(false);
            var userId = _currentUserService.UserId;

            var item = new WarehouseItem
            {
                Id = itemId,
                PartId = model.PartId,
                MovementType = WarehouseMovementKind.Issue,
                SourceType = WarehouseMovementKind.ManualIssue,
                DocumentNumber = documentNumber,
                AcceptedByName = TrimOrNull(model.AcceptedByName) ?? DefaultIssueRecipientName,
                CreatedByUserId = userId == Guid.Empty ? null : userId,
                Quantity = -model.Quantity,
                AddedAt = issueDate,
                CreatedAt = now,
                UpdatedAt = now,
                Comment = TrimOrNull(model.Comment),
            };

            await _dbContext.WarehouseItems.AddAsync(item, cancellationToken).ConfigureAwait(false);
            if (label is not null)
            {
                await AddWarehouseLabelItemAsync(item, label, -model.Quantity, issueDate, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            TempData["WarehouseMessage"] = $"Ручной расход {documentNumber} создан.";
            return RedirectToAction(nameof(Index), new { partId = model.PartId });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["WarehouseError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("manual-assembly-unit-issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualAssemblyUnitIssue(WarehouseAssemblyUnitIssueModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.AssemblyUnitName))
        {
            ModelState.AddModelError(nameof(model.AssemblyUnitName), "Укажите сборочный узел для ручного расхода.");
        }

        if (!ModelState.IsValid)
        {
            TempData["WarehouseError"] = "Проверьте данные расхода сборочного узла.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var assemblyUnit = await ResolveAssemblyUnitAsync(
                    model.AssemblyUnitId,
                    model.AssemblyUnitName,
                    createIfMissing: false,
                    cancellationToken)
                .ConfigureAwait(false);

            await EnsureAvailableBalanceAsync(null, assemblyUnit.Id, model.Quantity, cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var itemId = Guid.NewGuid();
            var issueDate = NormalizeToUtc(model.IssueDate == default ? DateTime.Today : model.IssueDate);
            var documentNumber = TrimOrNull(model.DocumentNumber) ?? BuildManualWarehouseDocumentNumber(issueDate, itemId, "ISS");
            var labelNumber = await ResolveAssemblyIssueLabelNumberAsync(assemblyUnit.Id, model.LabelNumber, model.Quantity, cancellationToken)
                .ConfigureAwait(false);
            var userId = _currentUserService.UserId;

            var item = new WarehouseItem
            {
                Id = itemId,
                AssemblyUnitId = assemblyUnit.Id,
                MovementType = WarehouseMovementKind.Issue,
                SourceType = WarehouseMovementKind.ManualIssue,
                DocumentNumber = documentNumber,
                AcceptedByName = TrimOrNull(model.AcceptedByName) ?? DefaultIssueRecipientName,
                CreatedByUserId = userId == Guid.Empty ? null : userId,
                Quantity = -model.Quantity,
                AddedAt = issueDate,
                CreatedAt = now,
                UpdatedAt = now,
                Comment = TrimOrNull(model.Comment),
            };

            await _dbContext.WarehouseItems.AddAsync(item, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(labelNumber))
            {
                await AddWarehouseLabelItemAsync(item, labelNumber, -model.Quantity, issueDate, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            TempData["WarehouseMessage"] = $"Расход сборочного узла {documentNumber} создан.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["WarehouseError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(WarehouseItemEditModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["WarehouseError"] = "Проверьте корректность введённых данных.";
            return RedirectToIndex(model);
        }

        try
        {
            if (model.Quantity < 0)
            {
                throw new InvalidOperationException("Количество не может быть отрицательным.");
            }

            var item = await _dbContext.WarehouseItems
                .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken)
                .ConfigureAwait(false);

            if (item is null)
            {
                throw new KeyNotFoundException("Запись склада не найдена.");
            }

            var trimmedComment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
            var addedAt = NormalizeToUtc(model.AddedAt);

            var normalizedQuantity = Math.Abs(model.Quantity);
            item.Quantity = item.MovementType == WarehouseMovementKind.Issue
                ? -normalizedQuantity
                : normalizedQuantity;
            item.AddedAt = addedAt;
            item.Comment = trimmedComment;
            item.DocumentNumber = TrimOrNull(model.DocumentNumber);
            item.ControlCardNumber = TrimOrNull(model.ControlCardNumber);
            item.ControllerName = TrimOrNull(model.ControllerName);
            item.MasterName = TrimOrNull(model.MasterName);
            item.AcceptedByName = TrimOrNull(model.AcceptedByName);
            item.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            TempData["WarehouseMessage"] = "Запись склада обновлена.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            TempData["WarehouseError"] = ex.Message;
        }

        return RedirectToIndex(model);
    }

    private RedirectToActionResult RedirectToIndex(WarehouseItemEditModel model)
    {
        var currentPage = model.FilterPage.HasValue && model.FilterPage.Value > 0 ? model.FilterPage.Value : 1;
        var currentPageSize = model.FilterPageSize.HasValue && model.FilterPageSize.Value > 0 ? model.FilterPageSize.Value : DefaultPageSize;

        return RedirectToAction(
            nameof(Index),
            new
            {
                partId = model.FilterPartId,
                partSearch = model.FilterPartSearch,
                movement = NormalizeMovementFilter(model.FilterMovement),
                page = currentPage,
                pageSize = currentPageSize,
            });
    }

    private async Task<WarehouseIndexViewModel> BuildIndexViewModelAsync(
        Guid? partId,
        string? partSearch,
        string? movement,
        string? statusMessage,
        string? errorMessage,
        string? autoPrintControlCardUrl,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedMovement = NormalizeMovementFilter(movement);
        var normalizedPartSearch = NormalizeSearchText(partSearch);
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var normalizedPage = page < 1 ? 1 : page;

        var parts = await _dbContext.Parts
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = NameWithCodeFormatter.getNameWithCode(x.Name, x.Code),
                Selected = partId.HasValue && x.Id == partId.Value,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        parts.Insert(0, new SelectListItem
        {
            Value = string.Empty,
            Text = "Все детали",
            Selected = !partId.HasValue,
        });

        var query = _dbContext.WarehouseItems
            .AsNoTracking()
            .Include(x => x.Part)
            .Include(x => x.AssemblyUnit)
            .Include(x => x.WarehouseLabelItems)
            .ThenInclude(x => x.WipLabel)
            .AsQueryable();

        query = ApplyItemFilter(query, partId, normalizedPartSearch);

        var totalMovementCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalQuantity = await query.SumAsync(x => (decimal?)x.Quantity, cancellationToken).ConfigureAwait(false) ?? 0m;
        var receiptMovementCount = await query
            .CountAsync(x => x.MovementType == WarehouseMovementKind.Receipt, cancellationToken)
            .ConfigureAwait(false);
        var issueMovementCount = await query
            .CountAsync(x => x.MovementType == WarehouseMovementKind.Issue, cancellationToken)
            .ConfigureAwait(false);
        var receiptQuantity = await query
            .Where(x => x.MovementType == WarehouseMovementKind.Receipt)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken)
            .ConfigureAwait(false) ?? 0m;
        var issueQuantity = Math.Abs(await query
            .Where(x => x.MovementType == WarehouseMovementKind.Issue)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken)
            .ConfigureAwait(false) ?? 0m);
        var manualReceiptCount = await query
            .CountAsync(x => x.MovementType == WarehouseMovementKind.Receipt && x.SourceType == WarehouseMovementKind.ManualReceipt, cancellationToken)
            .ConfigureAwait(false);
        var manualIssueCount = await query
            .CountAsync(x => x.MovementType == WarehouseMovementKind.Issue && x.SourceType == WarehouseMovementKind.ManualIssue, cancellationToken)
            .ConfigureAwait(false);
        var automaticReceiptCount = await query
            .CountAsync(x => x.MovementType == WarehouseMovementKind.Receipt && (x.SourceType == null || x.SourceType == WarehouseMovementKind.AutomaticTransfer), cancellationToken)
            .ConfigureAwait(false);

        var balanceRows = await SelectWarehouseRows(query)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var partGroups = BuildPartGroups(balanceRows);

        var journalQuery = ApplyMovementFilter(query, normalizedMovement);
        var totalItems = await journalQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);

        if (totalPages == 0)
        {
            normalizedPage = 1;
        }
        else if (normalizedPage > totalPages)
        {
            normalizedPage = totalPages;
        }

        var skip = (normalizedPage - 1) * normalizedPageSize;
        if (skip < 0)
        {
            skip = 0;
        }

        var items = await SelectWarehouseRows(journalQuery.OrderByDescending(x => x.AddedAt))
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var selectedPartText = parts.FirstOrDefault(x => x.Selected && !string.IsNullOrWhiteSpace(x.Value))?.Text;

        return new WarehouseIndexViewModel
        {
            SelectedPartId = partId,
            PartSearch = partId.HasValue ? selectedPartText ?? normalizedPartSearch : normalizedPartSearch,
            MovementFilter = normalizedMovement,
            Parts = parts,
            Items = items,
            PartGroups = partGroups,
            Areas = new[]
            {
                new WarehouseAreaViewModel
                {
                    Key = "sgdu",
                    Title = "Склад СГДУ",
                    Description = "Готовые детали и сборочные узлы после НЗП",
                    IsActive = true,
                    IsEnabled = true,
                },
                new WarehouseAreaViewModel
                {
                    Key = "components",
                    Title = "Комплектующие",
                    Description = "Будет выделено отдельным типом складской записи",
                    IsActive = false,
                    IsEnabled = false,
                },
            },
            MovementTypes = new[]
            {
                new WarehouseMovementTypeViewModel { Title = "Приход", IsEnabled = true },
                new WarehouseMovementTypeViewModel { Title = "Расход", IsEnabled = true },
            },
            MovementSources = new[]
            {
                new WarehouseMovementSourceViewModel { Title = "Ручной", IsEnabled = true },
                new WarehouseMovementSourceViewModel { Title = "Автоматический", IsEnabled = true },
            },
            ManualReceipt = new WarehouseManualReceiptModel
            {
                ReceiptDate = DateTime.Today,
                PrintControlCard = true,
            },
            AssemblyUnitReceipt = new WarehouseAssemblyUnitReceiptModel
            {
                ReceiptDate = DateTime.Today,
                PrintControlCard = true,
            },
            ManualIssue = new WarehouseManualIssueModel
            {
                IssueDate = DateTime.Today,
                AcceptedByName = DefaultIssueRecipientName,
            },
            AssemblyUnitIssue = new WarehouseAssemblyUnitIssueModel
            {
                IssueDate = DateTime.Today,
                AcceptedByName = DefaultIssueRecipientName,
            },
            TotalQuantity = totalQuantity,
            AutomaticReceiptCount = automaticReceiptCount,
            ManualReceiptCount = manualReceiptCount,
            ManualIssueCount = manualIssueCount,
            TotalMovementCount = totalMovementCount,
            ReceiptMovementCount = receiptMovementCount,
            IssueMovementCount = issueMovementCount,
            ReceiptQuantity = receiptQuantity,
            IssueQuantity = issueQuantity,
            BalanceGroupCount = partGroups.Length,
            AutoPrintControlCardUrl = autoPrintControlCardUrl,
            StatusMessage = statusMessage,
            ErrorMessage = errorMessage,
            CurrentPage = normalizedPage,
            PageSize = normalizedPageSize,
            TotalPages = totalPages,
        };
    }

    private static WarehousePartGroupViewModel[] BuildPartGroups(IReadOnlyCollection<WarehouseItemRowViewModel> rows)
    {
        return rows
            .GroupBy(x => new { x.PartId, x.AssemblyUnitId })
            .Select(group =>
            {
                var orderedItems = group
                    .OrderByDescending(item => item.AddedAt)
                    .ThenBy(item => item.CreatedAt)
                    .ToArray();

                var labelGroups = group
                    .SelectMany(item => item.LabelRows)
                    .Where(label => !string.IsNullOrWhiteSpace(label.LabelNumber))
                    .GroupBy(label => new { label.LabelId, label.LabelNumber })
                    .Select(labelGroup =>
                    {
                        var labelItems = labelGroup.ToList();
                        var firstLabel = labelItems
                            .OrderBy(x => x.AddedAt)
                            .First();

                        var updates = labelItems
                            .Where(x => x.UpdatedAt.HasValue)
                            .Select(x => x.UpdatedAt!.Value)
                            .ToList();

                        DateTime? lastUpdated = null;

                        if (updates.Count > 0)
                        {
                            lastUpdated = updates.Max();
                        }

                        return new WarehouseLabelGroupViewModel
                        {
                            LabelId = labelGroup.Key.LabelId,
                            LabelNumber = firstLabel.LabelNumber,
                            TotalQuantity = labelItems.Sum(x => x.Quantity),
                            FirstAddedAt = labelItems.Min(x => x.AddedAt),
                            LastUpdatedAt = lastUpdated,
                        };
                    })
                    .OrderByDescending(label => label.FirstAddedAt)
                    .ThenBy(label => label.LabelNumber, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();

                var latestAddedAt = orderedItems.Length > 0
                    ? orderedItems.First().AddedAt
                    : DateTime.MinValue;
                var lastItem = orderedItems.FirstOrDefault();

                return new
                {
                    LatestAddedAt = latestAddedAt,
                    Group = new WarehousePartGroupViewModel
                    {
                        PartId = group.Key.PartId,
                        AssemblyUnitId = group.Key.AssemblyUnitId,
                        ItemDisplay = orderedItems.Length > 0 ? orderedItems.First().ItemDisplay : string.Empty,
                        ItemKindTitle = orderedItems.Length > 0 ? orderedItems.First().ItemKindTitle : string.Empty,
                        TotalQuantity = group.Sum(item => item.QuantityImpact),
                        ReceiptQuantity = group
                            .Where(item => item.MovementType == WarehouseMovementKind.Receipt)
                            .Sum(item => item.Quantity),
                        IssueQuantity = group
                            .Where(item => item.MovementType == WarehouseMovementKind.Issue)
                            .Sum(item => item.Quantity),
                        MovementCount = group.Count(),
                        ReceiptCount = group.Count(item => item.MovementType == WarehouseMovementKind.Receipt),
                        IssueCount = group.Count(item => item.MovementType == WarehouseMovementKind.Issue),
                        LastMovementAt = latestAddedAt,
                        LastMovementTitle = lastItem?.MovementTitle ?? string.Empty,
                        LastSourceTitle = lastItem?.SourceTitle ?? string.Empty,
                        LastDocumentNumber = lastItem?.DocumentNumber,
                        LabelGroups = labelGroups,
                        Items = orderedItems,
                    },
                };
            })
            .OrderByDescending(x => x.LatestAddedAt)
            .ThenBy(x => x.Group.ItemDisplay, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Group)
            .ToArray();
    }

    private static IQueryable<WarehouseItemRowViewModel> SelectWarehouseRows(IQueryable<WarehouseItem> query)
    {
        return query.Select(x => new WarehouseItemRowViewModel
        {
            Id = x.Id,
            PartId = x.PartId,
            AssemblyUnitId = x.AssemblyUnitId,
            ItemDisplay = x.Part != null
                ? NameWithCodeFormatter.getNameWithCode(x.Part.Name, x.Part.Code)
                : x.AssemblyUnit != null
                    ? x.AssemblyUnit.Name
                    : "Без номенклатуры",
            ItemKindTitle = x.AssemblyUnitId.HasValue ? "Сборочный узел" : "Деталь",
            TransferId = x.TransferId,
            MovementType = x.MovementType,
            MovementTitle = x.MovementType == WarehouseMovementKind.Issue ? "Расход" : "Приход",
            SourceType = x.SourceType,
            SourceTitle = x.SourceType == WarehouseMovementKind.ManualReceipt
                ? "Ручной приход"
                : x.SourceType == WarehouseMovementKind.ManualIssue
                    ? "Ручной расход"
                    : "Авто из НЗП",
            DocumentNumber = x.DocumentNumber,
            ControlCardNumber = x.ControlCardNumber,
            ControllerName = x.ControllerName,
            MasterName = x.MasterName,
            AcceptedByName = x.AcceptedByName,
            Quantity = Math.Abs(x.Quantity),
            QuantityImpact = x.Quantity,
            AddedAt = x.AddedAt,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            Comment = x.Comment,
            LabelRows = x.WarehouseLabelItems
                .OrderByDescending(labelItem => labelItem.AddedAt)
                .Select(labelItem => new WarehouseLabelRowViewModel
                {
                    LabelId = labelItem.WipLabelId,
                    LabelNumber = labelItem.WipLabel != null ? labelItem.WipLabel.Number : (labelItem.LabelNumber ?? string.Empty),
                    Quantity = labelItem.Quantity,
                    AddedAt = labelItem.AddedAt,
                    UpdatedAt = labelItem.UpdatedAt,
                })
                .ToArray(),
        });
    }

    private static IQueryable<WarehouseItem> ApplyItemFilter(IQueryable<WarehouseItem> query, Guid? partId, string partSearch)
    {
        if (partId.HasValue)
        {
            return query.Where(x => x.PartId == partId.Value);
        }

        foreach (var term in NormalizeSearchTerms(partSearch))
        {
            var searchTerm = term.ToLower();
            query = query.Where(x =>
                (x.Part != null
                    && (x.Part.Name.ToLower().Contains(searchTerm)
                        || (x.Part.Code != null && x.Part.Code.ToLower().Contains(searchTerm))))
                || (x.AssemblyUnit != null && x.AssemblyUnit.Name.ToLower().Contains(searchTerm))
                || (x.DocumentNumber != null && x.DocumentNumber.ToLower().Contains(searchTerm))
                || (x.ControlCardNumber != null && x.ControlCardNumber.ToLower().Contains(searchTerm)));
        }

        return query;
    }

    private static IQueryable<WarehouseItem> ApplyMovementFilter(IQueryable<WarehouseItem> query, string movement)
    {
        return movement switch
        {
            MovementFilterReceipts => query.Where(x => x.MovementType == WarehouseMovementKind.Receipt),
            MovementFilterIssues => query.Where(x => x.MovementType == WarehouseMovementKind.Issue),
            _ => query,
        };
    }

    private static string NormalizeMovementFilter(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            MovementFilterReceipts or "receipt" or "income" => MovementFilterReceipts,
            MovementFilterIssues or "issue" or "outcome" => MovementFilterIssues,
            _ => MovementFilterAll,
        };
    }

    private static string NormalizeSearchText(string? value)
    {
        return string.Join(' ', NormalizeSearchTerms(value));
    }

    private static string[] NormalizeSearchTerms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split((char[]?)null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 0)
            .ToArray();
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    private async Task<WarehouseAssemblyUnit> ResolveAssemblyUnitAsync(
        Guid? assemblyUnitId,
        string? assemblyUnitName,
        bool createIfMissing,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeAssemblyUnitName(assemblyUnitName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Наименование сборочного узла обязательно.");
        }

        WarehouseAssemblyUnit? assemblyUnit = null;

        if (assemblyUnitId.HasValue && assemblyUnitId.Value != Guid.Empty)
        {
            assemblyUnit = await _dbContext.WarehouseAssemblyUnits
                .FirstOrDefaultAsync(x => x.Id == assemblyUnitId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (assemblyUnit is null)
            {
                throw new KeyNotFoundException("Сборочный узел не найден.");
            }
        }
        else
        {
            assemblyUnit = await _dbContext.WarehouseAssemblyUnits
                .FirstOrDefaultAsync(x => x.NormalizedName == normalizedName, cancellationToken)
                .ConfigureAwait(false);
        }

        if (assemblyUnit is not null)
        {
            return assemblyUnit;
        }

        if (!createIfMissing)
        {
            throw new KeyNotFoundException("Сборочный узел для расхода не найден. Сначала оформите приход.");
        }

        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;

        assemblyUnit = new WarehouseAssemblyUnit
        {
            Id = Guid.NewGuid(),
            Name = assemblyUnitName!.Trim(),
            NormalizedName = normalizedName,
            CreatedByUserId = userId == Guid.Empty ? null : userId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _dbContext.WarehouseAssemblyUnits.AddAsync(assemblyUnit, cancellationToken).ConfigureAwait(false);
        return assemblyUnit;
    }

    private async Task<string?> ResolveAssemblyReceiptLabelNumberAsync(Guid assemblyUnitId, string? labelNumber, CancellationToken cancellationToken)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(labelNumber) ? null : NormalizeLabelNumber(labelNumber);
        if (string.IsNullOrWhiteSpace(normalizedNumber))
        {
            return null;
        }

        var isUsedByOtherAssembly = await _dbContext.WarehouseLabelItems
            .AsNoTracking()
            .AnyAsync(
                x => x.LabelNumber == normalizedNumber &&
                     x.WarehouseItem != null &&
                     x.WarehouseItem.AssemblyUnitId.HasValue &&
                     x.WarehouseItem.AssemblyUnitId != assemblyUnitId,
                cancellationToken)
            .ConfigureAwait(false);

        if (isUsedByOtherAssembly)
        {
            throw new InvalidOperationException($"Ярлык {normalizedNumber} уже используется другим сборочным узлом.");
        }

        return normalizedNumber;
    }

    private async Task<string?> ResolveAssemblyIssueLabelNumberAsync(Guid assemblyUnitId, string? labelNumber, decimal quantity, CancellationToken cancellationToken)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(labelNumber) ? null : NormalizeLabelNumber(labelNumber);
        if (string.IsNullOrWhiteSpace(normalizedNumber))
        {
            return null;
        }

        var available = await _dbContext.WarehouseLabelItems
            .AsNoTracking()
            .Where(x =>
                x.LabelNumber == normalizedNumber &&
                x.WarehouseItem != null &&
                x.WarehouseItem.AssemblyUnitId == assemblyUnitId)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        if (available + 0.000001m < quantity)
        {
            throw new InvalidOperationException($"Недостаточно остатка по ярлыку {normalizedNumber}. Доступно {available:0.###}, требуется {quantity:0.###}.");
        }

        return normalizedNumber;
    }

    private async Task<WipLabel?> ResolveReceiptLabelAsync(
        Guid partId,
        Guid? labelId,
        string? labelNumber,
        decimal quantity,
        DateTime receiptDate,
        CancellationToken cancellationToken)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(labelNumber) ? null : NormalizeLabelNumber(labelNumber);
        if ((!labelId.HasValue || labelId.Value == Guid.Empty) && string.IsNullOrWhiteSpace(normalizedNumber))
        {
            return null;
        }

        var labelDate = DateTime.SpecifyKind(receiptDate.Date, DateTimeKind.Utc);
        WipLabel? label = null;

        if (labelId.HasValue && labelId.Value != Guid.Empty)
        {
            label = await _dbContext.WipLabels
                .FirstOrDefaultAsync(x => x.Id == labelId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (label is null)
            {
                throw new KeyNotFoundException("Выбранный ярлык не найден.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(normalizedNumber))
        {
            label = await _dbContext.WipLabels
                .FirstOrDefaultAsync(x => x.Number == normalizedNumber && x.LabelYear == labelDate.Year, cancellationToken)
                .ConfigureAwait(false);
        }

        if (label is null)
        {
            label = CreateWarehouseLabel(partId, normalizedNumber!, quantity, labelDate, assigned: true);
            await _dbContext.WipLabels.AddAsync(label, cancellationToken).ConfigureAwait(false);
            return label;
        }

        if (!string.IsNullOrWhiteSpace(normalizedNumber) && !string.Equals(label.Number, normalizedNumber, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Выбранный ярлык {label.Number} не совпадает с номером {normalizedNumber}.");
        }

        if (label.PartId != partId)
        {
            throw new InvalidOperationException($"Ярлык {label.Number} относится к другой детали.");
        }

        if (label.Status != WipLabelStatus.Active)
        {
            throw new InvalidOperationException($"Ярлык {label.Number} не активен.");
        }

        if (label.IsAssigned)
        {
            throw new InvalidOperationException($"Ярлык {label.Number} уже назначен.");
        }

        if (label.LabelYear != labelDate.Year)
        {
            throw new InvalidOperationException($"Ярлык {label.Number} относится к году {label.LabelYear}, а приход оформляется за {labelDate.Year} год.");
        }

        if (Math.Abs(label.Quantity - quantity) > 0.000001m)
        {
            throw new InvalidOperationException($"Количество ярлыка {label.Number} ({label.Quantity:0.###}) не совпадает с количеством прихода ({quantity:0.###}).");
        }

        label.IsAssigned = true;
        label.CurrentSectionId = WarehouseDefaults.SectionId;
        label.CurrentOpNumber = WarehouseDefaults.OperationNumber;
        label.RemainingQuantity = quantity;
        EnsureLabelIdentity(label);
        return label;
    }

    private async Task<WipLabel?> ResolveIssueLabelAsync(
        Guid partId,
        Guid? labelId,
        string? labelNumber,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        var normalizedNumber = string.IsNullOrWhiteSpace(labelNumber) ? null : NormalizeLabelNumber(labelNumber);
        if ((!labelId.HasValue || labelId.Value == Guid.Empty) && string.IsNullOrWhiteSpace(normalizedNumber))
        {
            return null;
        }

        WipLabel? label;
        if (labelId.HasValue && labelId.Value != Guid.Empty)
        {
            label = await _dbContext.WipLabels
                .FirstOrDefaultAsync(x => x.Id == labelId.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            label = await _dbContext.WipLabels
                .Where(x => x.PartId == partId && x.Number == normalizedNumber)
                .OrderByDescending(x => x.LabelYear)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (label is null)
        {
            throw new KeyNotFoundException("Ярлык для расхода не найден.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedNumber) && !string.Equals(label.Number, normalizedNumber, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Выбранный ярлык {label.Number} не совпадает с номером {normalizedNumber}.");
        }

        if (label.PartId != partId)
        {
            throw new InvalidOperationException($"Ярлык {label.Number} относится к другой детали.");
        }

        if (label.Status != WipLabelStatus.Active)
        {
            throw new InvalidOperationException($"Ярлык {label.Number} не активен.");
        }

        var available = await _dbContext.WarehouseLabelItems
            .AsNoTracking()
            .Where(x => x.WipLabelId == label.Id)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        if (available + 0.000001m < quantity)
        {
            throw new InvalidOperationException($"Недостаточно остатка по ярлыку {label.Number}. Доступно {available:0.###}, требуется {quantity:0.###}.");
        }

        return label;
    }

    private static WipLabel CreateWarehouseLabel(Guid partId, string number, decimal quantity, DateTime labelDate, bool assigned)
    {
        var label = new WipLabel
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            LabelDate = labelDate,
            LabelYear = labelDate.Year,
            Quantity = quantity,
            RemainingQuantity = quantity,
            Number = number,
            IsAssigned = assigned,
            Status = WipLabelStatus.Active,
            CurrentSectionId = assigned ? WarehouseDefaults.SectionId : null,
            CurrentOpNumber = assigned ? WarehouseDefaults.OperationNumber : null,
            RootLabelId = Guid.Empty,
            ParentLabelId = null,
            RootNumber = string.Empty,
            Suffix = 0,
        };

        EnsureLabelIdentity(label);
        return label;
    }

    private static void EnsureLabelIdentity(WipLabel label)
    {
        if (label.Id == Guid.Empty)
        {
            label.Id = Guid.NewGuid();
        }

        var parsedNumber = WipLabelInvariants.ParseNumber(label.Number);
        label.RootLabelId = label.RootLabelId == Guid.Empty ? label.Id : label.RootLabelId;
        label.RootNumber = string.IsNullOrWhiteSpace(label.RootNumber) ? parsedNumber.RootNumber : label.RootNumber;
        label.Suffix = parsedNumber.Suffix;
    }

    private async Task AddWarehouseLabelItemAsync(
        WarehouseItem warehouseItem,
        WipLabel label,
        decimal quantity,
        DateTime addedAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var labelItem = new WarehouseLabelItem
        {
            Id = Guid.NewGuid(),
            WarehouseItemId = warehouseItem.Id,
            WipLabelId = label.Id,
            LabelNumber = label.Number,
            Quantity = quantity,
            AddedAt = addedAt,
            UpdatedAt = now,
        };

        await _dbContext.WarehouseLabelItems.AddAsync(labelItem, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddWarehouseLabelItemAsync(
        WarehouseItem warehouseItem,
        string labelNumber,
        decimal quantity,
        DateTime addedAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var labelItem = new WarehouseLabelItem
        {
            Id = Guid.NewGuid(),
            WarehouseItemId = warehouseItem.Id,
            WipLabelId = null,
            LabelNumber = labelNumber,
            Quantity = quantity,
            AddedAt = addedAt,
            UpdatedAt = now,
        };

        await _dbContext.WarehouseLabelItems.AddAsync(labelItem, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureAvailableBalanceAsync(Guid? partId, Guid? assemblyUnitId, decimal quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Количество расхода должно быть больше нуля.");
        }

        IQueryable<WarehouseItem> query = _dbContext.WarehouseItems.AsNoTracking();

        if (partId.HasValue)
        {
            query = query.Where(x => x.PartId == partId.Value);
        }
        else if (assemblyUnitId.HasValue)
        {
            query = query.Where(x => x.AssemblyUnitId == assemblyUnitId.Value);
        }
        else
        {
            throw new ArgumentException("Не выбрана номенклатура для проверки остатка.");
        }

        var available = await query.SumAsync(x => (decimal?)x.Quantity, cancellationToken).ConfigureAwait(false) ?? 0m;
        if (available + 0.000001m < quantity)
        {
            throw new InvalidOperationException($"Недостаточно остатка на складе. Доступно {available:0.###}, требуется {quantity:0.###}.");
        }
    }

    private static string NormalizeAssemblyUnitName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string
            .Join(' ', value.Split((char[]?)null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    private static string BuildManualWarehouseDocumentNumber(DateTime receiptDate, Guid itemId)
    {
        return BuildManualWarehouseDocumentNumber(receiptDate, itemId, "MAN");
    }

    private static string BuildManualWarehouseDocumentNumber(DateTime receiptDate, Guid itemId, string prefix)
    {
        var suffix = itemId.ToString("N")[..8].ToUpperInvariant();
        return $"{prefix}-{receiptDate:yyyyMMdd}-{suffix}";
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeLabelNumber(string value)
    {
        var parsed = WipLabelInvariants.ParseNumber(value);
        if (parsed.RootNumber.Length is < 1 or > 32 || !parsed.RootNumber.All(char.IsDigit))
        {
            throw new InvalidOperationException("Номер ярлыка должен быть в формате 12345 или 12345/1.");
        }

        if (!int.TryParse(parsed.RootNumber, out var root) || root <= 0)
        {
            throw new InvalidOperationException("Номер ярлыка должен быть положительным числом.");
        }

        var normalizedRoot = root.ToString("D5");
        return WipLabelInvariants.FormatNumber(normalizedRoot, parsed.Suffix);
    }
}
