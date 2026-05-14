using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;
using UchetNZP.Shared;
using UchetNZP.Web.Models;

namespace UchetNZP.Web.Controllers;

[Route("wip/quality")]
public class WipQualityController : Controller
{
    private readonly AppDbContext _dbContext;

    public WipQualityController(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var balances = await _dbContext.WipBalances
            .AsNoTracking()
            .Where(x => x.Quantity > 0m)
            .Include(x => x.Part)
            .Include(x => x.Section)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (balances.Count == 0)
        {
            return View("~/Views/Wip/Quality.cshtml", new WipQualityIndexViewModel(Array.Empty<WipQualityQueueItemViewModel>()));
        }

        var partIds = balances.Select(x => x.PartId).Distinct().ToArray();
        var sectionIds = balances.Select(x => x.SectionId).Distinct().ToArray();
        var opNumbers = balances.Select(x => x.OpNumber).Distinct().ToArray();

        var routes = await _dbContext.PartRoutes
            .AsNoTracking()
            .Where(x => partIds.Contains(x.PartId) && sectionIds.Contains(x.SectionId) && opNumbers.Contains(x.OpNumber))
            .Include(x => x.Operation)
            .Include(x => x.Section)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var qualityRoutes = routes
            .Where(route => IsQualityRoute(route.Section?.Name, route.Operation?.Name))
            .ToDictionary(route => (route.PartId, route.SectionId, route.OpNumber), route => route);

        if (qualityRoutes.Count == 0)
        {
            return View("~/Views/Wip/Quality.cshtml", new WipQualityIndexViewModel(Array.Empty<WipQualityQueueItemViewModel>()));
        }

        var labels = await _dbContext.WipLabels
            .AsNoTracking()
            .Where(x =>
                partIds.Contains(x.PartId) &&
                x.CurrentSectionId.HasValue &&
                sectionIds.Contains(x.CurrentSectionId.Value) &&
                x.CurrentOpNumber.HasValue &&
                opNumbers.Contains(x.CurrentOpNumber.Value) &&
                x.RemainingQuantity > 0m)
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var labelsByOperation = labels
            .GroupBy(x => (x.PartId, SectionId: x.CurrentSectionId!.Value, OpNumber: x.CurrentOpNumber!.Value))
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<WipQualityLabelViewModel>)x
                    .Select(label => new WipQualityLabelViewModel(
                        label.Id,
                        label.Number,
                        string.IsNullOrWhiteSpace(label.RootNumber) ? label.Number.Split('/')[0] : label.RootNumber,
                        label.RemainingQuantity))
                    .ToList());

        var items = balances
            .Where(balance => qualityRoutes.ContainsKey((balance.PartId, balance.SectionId, balance.OpNumber)))
            .OrderBy(balance => balance.Part != null ? balance.Part.Name : string.Empty)
            .ThenBy(balance => balance.OpNumber)
            .Select(balance =>
            {
                var key = (balance.PartId, balance.SectionId, balance.OpNumber);
                var route = qualityRoutes[key];
                labelsByOperation.TryGetValue(key, out var operationLabels);
                operationLabels ??= Array.Empty<WipQualityLabelViewModel>();

                var firstRoot = operationLabels.FirstOrDefault()?.RootNumber;
                var defectPreview = string.IsNullOrWhiteSpace(firstRoot)
                    ? "будет назначено при фиксации брака"
                    : $"{firstRoot} Б";

                return new WipQualityQueueItemViewModel(
                    balance.PartId,
                    balance.Part != null ? balance.Part.Name : string.Empty,
                    balance.Part?.Code,
                    balance.SectionId,
                    route.Section?.Name ?? balance.Section?.Name ?? string.Empty,
                    OperationNumber.Format(balance.OpNumber),
                    route.Operation?.Name ?? string.Empty,
                    balance.Quantity,
                    operationLabels,
                    defectPreview,
                    BuildReturnOperationHint(routes, balance.PartId, balance.OpNumber));
            })
            .ToList();

        return View("~/Views/Wip/Quality.cshtml", new WipQualityIndexViewModel(items));
    }

    private static bool IsQualityRoute(string? sectionName, string? operationName)
    {
        var text = $"{sectionName} {operationName}".ToLowerInvariant();
        return text.Contains("отк", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("контрол", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReturnOperationHint(IReadOnlyList<PartRoute> routes, Guid partId, int currentOpNumber)
    {
        var previous = routes
            .Where(x => x.PartId == partId && x.OpNumber < currentOpNumber)
            .OrderByDescending(x => x.OpNumber)
            .FirstOrDefault();

        if (previous is null)
        {
            return "операция возврата будет выбрана вручную";
        }

        var operationName = previous.Operation?.Name;
        return string.IsNullOrWhiteSpace(operationName)
            ? OperationNumber.Format(previous.OpNumber)
            : $"{OperationNumber.Format(previous.OpNumber)} {operationName}";
    }
}
