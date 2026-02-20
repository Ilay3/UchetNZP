using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("warehouse")]
public class WarehouseController : Controller
{
    private const int DefaultPageSize = 20;

    private readonly AppDbContext _dbContext;

    public WarehouseController(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? partId, int page = 1, int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        await _dbContext.CleanupWarehouseAsync(DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

        var statusMessage = TempData["WarehouseMessage"] as string;
        var errorMessage = TempData["WarehouseError"] as string;

        var model = await BuildIndexViewModelAsync(partId, statusMessage, errorMessage, page, pageSize, cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(WarehouseItemEditModel model, CancellationToken cancellationToken)
    {
        await _dbContext.CleanupWarehouseAsync(DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

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

            item.Quantity = model.Quantity;
            item.AddedAt = addedAt;
            item.Comment = trimmedComment;
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
                page = currentPage,
                pageSize = currentPageSize,
            });
    }

    private async Task<WarehouseIndexViewModel> BuildIndexViewModelAsync(
        Guid? partId,
        string? statusMessage,
        string? errorMessage,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : pageSize;
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
            .Include(x => x.WarehouseLabelItems)
            .ThenInclude(x => x.WipLabel)
            .AsQueryable();

        if (partId.HasValue)
        {
            query = query.Where(x => x.PartId == partId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalQuantity = await query.SumAsync(x => (decimal?)x.Quantity, cancellationToken).ConfigureAwait(false) ?? 0m;
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

        var items = await query
            .OrderByDescending(x => x.AddedAt)
            .ThenBy(x => x.Part!.Name)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(x => new WarehouseItemRowViewModel
            {
                Id = x.Id,
                PartId = x.PartId,
                PartDisplay = NameWithCodeFormatter.getNameWithCode(x.Part!.Name, x.Part.Code),
                Quantity = x.Quantity,
                AddedAt = x.AddedAt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                Comment = x.Comment,
                LabelRows = x.WarehouseLabelItems
                    .OrderByDescending(labelItem => labelItem.AddedAt)
                    .Select(labelItem => new WarehouseLabelRowViewModel
                    {
                        LabelId = labelItem.WipLabelId,
                        LabelNumber = labelItem.WipLabel != null ? labelItem.WipLabel.Number : string.Empty,
                        Quantity = labelItem.Quantity,
                        AddedAt = labelItem.AddedAt,
                        UpdatedAt = labelItem.UpdatedAt,
                    })
                    .ToArray(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var partGroups = items
            .GroupBy(x => x.PartId)
            .Select(group =>
            {
                var orderedItems = group
                    .OrderByDescending(item => item.AddedAt)
                    .ThenBy(item => item.CreatedAt)
                    .ToArray();

                var labelGroups = group
                    .SelectMany(item => item.LabelRows)
                    .GroupBy(label => label.LabelId)
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
                            LabelId = labelGroup.Key,
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

                return new
                {
                    LatestAddedAt = latestAddedAt,
                    Group = new WarehousePartGroupViewModel
                    {
                        PartId = group.Key,
                        PartDisplay = orderedItems.Length > 0 ? orderedItems.First().PartDisplay : string.Empty,
                        TotalQuantity = group.Sum(item => item.Quantity),
                        LabelGroups = labelGroups,
                        Items = orderedItems,
                    },
                };
            })
            .OrderByDescending(x => x.LatestAddedAt)
            .ThenBy(x => x.Group.PartDisplay, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Group)
            .ToArray();

        return new WarehouseIndexViewModel
        {
            SelectedPartId = partId,
            Parts = parts,
            Items = items,
            PartGroups = partGroups,
            TotalQuantity = totalQuantity,
            StatusMessage = statusMessage,
            ErrorMessage = errorMessage,
            CurrentPage = normalizedPage,
            PageSize = normalizedPageSize,
            TotalPages = totalPages,
        };
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
}
