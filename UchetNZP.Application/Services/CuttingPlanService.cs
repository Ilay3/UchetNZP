using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UchetNZP.Application.Abstractions;
using UchetNZP.Application.Contracts.Cutting;
using UchetNZP.Domain.Entities;
using UchetNZP.Infrastructure.Data;

namespace UchetNZP.Application.Services;

public class CuttingPlanService(AppDbContext dbContext) : ICuttingPlanService
{
    public async Task<CuttingPlanResultDto> BuildAndSaveAsync(SaveCuttingPlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((request.Linear is null) == (request.Sheet is null))
        {
            throw new ArgumentException("Нужно передать либо 1D, либо 2D параметры.", nameof(request));
        }

        var requirementExists = await dbContext.MetalRequirements
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.MetalRequirementId, cancellationToken)
            .ConfigureAwait(false);
        if (!requirementExists)
        {
            throw new KeyNotFoundException("Требование металла не найдено.");
        }

        var kind = request.Linear is not null ? CuttingPlanKind.OneDimensional : CuttingPlanKind.TwoDimensional;
        var payload = JsonSerializer.Serialize(request);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var currentPlans = await dbContext.CuttingPlans
            .Where(x => x.MetalRequirementId == request.MetalRequirementId && x.Kind == kind && x.IsCurrent)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nextVersion = await dbContext.CuttingPlans
            .Where(x => x.MetalRequirementId == request.MetalRequirementId && x.Kind == kind)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0;

        foreach (var plan in currentPlans)
        {
            plan.IsCurrent = false;
        }

        var computed = request.Linear is not null
            ? BuildLinearPlan(request.Linear)
            : BuildSheetPlan(request.Sheet!);

        var entity = new CuttingPlan
        {
            Id = Guid.NewGuid(),
            MetalRequirementId = request.MetalRequirementId,
            Kind = kind,
            Version = nextVersion + 1,
            InputHash = hash,
            ParametersJson = payload,
            UtilizationPercent = computed.UtilizationPercent,
            WastePercent = computed.WastePercent,
            CutCount = computed.CutCount,
            BusinessResidual = computed.BusinessResidual,
            ScrapResidual = computed.ScrapResidual,
            CreatedAt = DateTime.UtcNow,
            IsCurrent = true,
            ExecutionStatus = "Не выполнено",
            Items = computed.Items,
        };

        dbContext.CuttingPlans.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        var stocks = entity.Items
            .GroupBy(x => x.StockIndex)
            .OrderBy(x => x.Key)
            .Select(g => new CuttingPlanStockDto(
                g.Key,
                g.OrderBy(x => x.Sequence)
                    .Select(x => new CuttingPlanPlacementDto(x.ItemType, x.Length, x.Width, x.Height, x.PositionX, x.PositionY, x.Rotated, x.Quantity))
                    .ToList()))
            .ToList();

        return new CuttingPlanResultDto(
            entity.Id,
            entity.Version,
            entity.Kind.ToString(),
            entity.UtilizationPercent,
            entity.WastePercent,
            entity.CutCount,
            entity.BusinessResidual,
            entity.ScrapResidual,
            stocks);
    }

    private static ComputedPlan BuildLinearPlan(LinearCutRequest request)
    {
        var parts = request.Parts
            .SelectMany(x => Enumerable.Repeat(x.Length, x.Quantity))
            .OrderByDescending(x => x)
            .ToList();

        var bins = new List<LinearBin>();
        foreach (var part in parts)
        {
            var bin = bins.FirstOrDefault(x => x.Remaining >= part + request.Kerf);
            if (bin is null)
            {
                bin = new LinearBin(request.StockLength);
                bins.Add(bin);
            }

            bin.Parts.Add(part);
            bin.Remaining -= (part + request.Kerf);
        }

        ImproveLinear(bins, request.StockLength, request.Kerf);

        var items = new List<CuttingPlanItem>();
        decimal used = 0m;
        decimal total = bins.Count * request.StockLength;
        decimal businessResidual = 0m;
        decimal scrapResidual = 0m;
        var cuts = 0;

        for (var stockIndex = 0; stockIndex < bins.Count; stockIndex++)
        {
            var bin = bins[stockIndex];
            var cursor = 0m;

            for (var i = 0; i < bin.Parts.Count; i++)
            {
                var length = bin.Parts[i];
                items.Add(new CuttingPlanItem
                {
                    Id = Guid.NewGuid(),
                    StockIndex = stockIndex,
                    Sequence = i,
                    ItemType = "part",
                    Length = length,
                    Quantity = 1,
                    PositionX = cursor,
                });
                cursor += length + request.Kerf;
                used += length;
                cuts++;
            }

            var residual = request.StockLength - cursor;
            var residualType = residual >= request.MinBusinessResidual ? "business_residual" : "scrap_residual";
            if (residual > 0)
            {
                items.Add(new CuttingPlanItem
                {
                    Id = Guid.NewGuid(),
                    StockIndex = stockIndex,
                    Sequence = bin.Parts.Count,
                    ItemType = residualType,
                    Length = residual,
                    PositionX = cursor,
                    Quantity = 1,
                });
            }

            if (residualType == "business_residual")
            {
                businessResidual += Math.Max(0, residual);
            }
            else
            {
                scrapResidual += Math.Max(0, residual);
            }
        }

        var utilization = total == 0 ? 0 : used / total * 100m;
        return new ComputedPlan(items, Math.Round(utilization, 3), Math.Round(100m - utilization, 3), cuts, businessResidual, scrapResidual);
    }

    private static void ImproveLinear(List<LinearBin> bins, decimal stockLength, decimal kerf)
    {
        var improved = true;
        while (improved)
        {
            improved = false;
            for (var i = 0; i < bins.Count; i++)
            {
                for (var j = i + 1; j < bins.Count; j++)
                {
                    var a = bins[i];
                    var b = bins[j];
                    foreach (var pa in a.Parts.ToList())
                    {
                        foreach (var pb in b.Parts.ToList())
                        {
                            var newA = a.Parts.Sum() - pa + pb + kerf * a.Parts.Count;
                            var newB = b.Parts.Sum() - pb + pa + kerf * b.Parts.Count;
                            if (newA > stockLength || newB > stockLength)
                            {
                                continue;
                            }

                            var oldSpread = Math.Abs(a.Remaining - b.Remaining);
                            var newSpread = Math.Abs(stockLength - newA - (stockLength - newB));
                            if (newSpread + 0.001m < oldSpread)
                            {
                                a.Parts.Remove(pa);
                                a.Parts.Add(pb);
                                b.Parts.Remove(pb);
                                b.Parts.Add(pa);
                                a.Remaining = stockLength - (a.Parts.Sum() + kerf * a.Parts.Count);
                                b.Remaining = stockLength - (b.Parts.Sum() + kerf * b.Parts.Count);
                                improved = true;
                                goto ContinueOuter;
                            }
                        }
                    }
                }
            }

        ContinueOuter:
            _ = 0;
        }
    }

    private static ComputedPlan BuildSheetPlan(SheetCutRequest request)
    {
        var pieces = request.Parts
            .SelectMany(x => Enumerable.Range(0, x.Quantity).Select(_ => (x.Width, x.Height)))
            .OrderByDescending(x => x.Width * x.Height)
            .ThenByDescending(x => Math.Max(x.Width, x.Height))
            .ToList();

        var sheets = new List<SheetState>();
        foreach (var piece in pieces)
        {
            if (!TryPlacePiece(sheets, piece.Width, piece.Height, request, out var placement))
            {
                var sheet = new SheetState(request.StockWidth, request.StockHeight, request.Margin);
                sheets.Add(sheet);
                if (!TryPlaceIntoSheet(sheet, piece.Width, piece.Height, request, out placement))
                {
                    throw new InvalidOperationException("Деталь не помещается в лист с учётом отступов.");
                }
            }

            placement!.ItemType = "part";
            placement.Quantity = 1;
        }

        var items = new List<CuttingPlanItem>();
        var cutCount = 0;
        var usedArea = 0m;
        var totalArea = sheets.Count * request.StockWidth * request.StockHeight;
        decimal businessResidual = 0m;
        decimal scrapResidual = 0m;

        for (var i = 0; i < sheets.Count; i++)
        {
            var sequence = 0;
            foreach (var part in sheets[i].Placed)
            {
                items.Add(new CuttingPlanItem
                {
                    Id = Guid.NewGuid(),
                    StockIndex = i,
                    Sequence = sequence++,
                    ItemType = "part",
                    Width = part.Width,
                    Height = part.Height,
                    PositionX = part.X,
                    PositionY = part.Y,
                    Rotated = part.Rotated,
                    Quantity = 1,
                });
                usedArea += part.Width * part.Height;
                cutCount += 2;
            }

            foreach (var free in sheets[i].Free.Where(x => x.W > 0 && x.H > 0))
            {
                var area = free.W * free.H;
                var type = area >= (request.StockWidth * request.StockHeight * 0.1m) ? "business_residual" : "scrap_residual";
                items.Add(new CuttingPlanItem
                {
                    Id = Guid.NewGuid(),
                    StockIndex = i,
                    Sequence = sequence++,
                    ItemType = type,
                    Width = free.W,
                    Height = free.H,
                    PositionX = free.X,
                    PositionY = free.Y,
                    Quantity = 1,
                });
                if (type == "business_residual")
                {
                    businessResidual += area;
                }
                else
                {
                    scrapResidual += area;
                }
            }
        }

        var utilization = totalArea == 0 ? 0 : usedArea / totalArea * 100m;
        return new ComputedPlan(items, Math.Round(utilization, 3), Math.Round(100m - utilization, 3), cutCount, businessResidual, scrapResidual);
    }

    private static bool TryPlacePiece(List<SheetState> sheets, decimal width, decimal height, SheetCutRequest request, out CuttingPlanItem? item)
    {
        foreach (var sheet in sheets)
        {
            if (TryPlaceIntoSheet(sheet, width, height, request, out item))
            {
                return true;
            }
        }

        item = null;
        return false;
    }

    private static bool TryPlaceIntoSheet(SheetState sheet, decimal width, decimal height, SheetCutRequest request, out CuttingPlanItem? item)
    {
        var candidates = new List<(FreeRect Rect, bool Rotated, decimal W, decimal H, decimal Waste)>();
        foreach (var rect in sheet.Free)
        {
            TryCandidate(rect, width, height, false);
            if (request.AllowRotation)
            {
                TryCandidate(rect, height, width, true);
            }
        }

        if (candidates.Count == 0)
        {
            item = null;
            return false;
        }

        var selected = candidates.OrderBy(x => x.Waste).First();
        sheet.Free.Remove(selected.Rect);

        var inflateW = selected.W + request.Gap;
        var inflateH = selected.H + request.Gap;

        var right = new FreeRect(selected.Rect.X + inflateW, selected.Rect.Y, selected.Rect.W - inflateW, inflateH);
        var top = new FreeRect(selected.Rect.X, selected.Rect.Y + inflateH, selected.Rect.W, selected.Rect.H - inflateH);

        if (right.W > 0 && right.H > 0)
        {
            sheet.Free.Add(right);
        }

        if (top.W > 0 && top.H > 0)
        {
            sheet.Free.Add(top);
        }

        var part = new PlacedPart(selected.Rect.X, selected.Rect.Y, selected.W, selected.H, selected.Rotated);
        sheet.Placed.Add(part);

        item = new CuttingPlanItem
        {
            Id = Guid.NewGuid(),
            Width = selected.W,
            Height = selected.H,
            PositionX = selected.Rect.X,
            PositionY = selected.Rect.Y,
            Rotated = selected.Rotated,
        };
        return true;

        void TryCandidate(FreeRect rect, decimal partW, decimal partH, bool rotated)
        {
            var reqW = partW + request.Gap;
            var reqH = partH + request.Gap;
            if (reqW > rect.W || reqH > rect.H)
            {
                return;
            }

            candidates.Add((rect, rotated, partW, partH, (rect.W * rect.H) - (partW * partH)));
        }
    }

    private sealed record ComputedPlan(
        List<CuttingPlanItem> Items,
        decimal UtilizationPercent,
        decimal WastePercent,
        int CutCount,
        decimal BusinessResidual,
        decimal ScrapResidual);

    private sealed class LinearBin(decimal stockLength)
    {
        public List<decimal> Parts { get; } = new();
        public decimal Remaining { get; set; } = stockLength;
    }

    private sealed class SheetState
    {
        public SheetState(decimal stockWidth, decimal stockHeight, decimal margin)
        {
            Free.Add(new FreeRect(margin, margin, stockWidth - (2 * margin), stockHeight - (2 * margin)));
        }

        public List<PlacedPart> Placed { get; } = new();
        public List<FreeRect> Free { get; } = new();
    }

    private sealed record FreeRect(decimal X, decimal Y, decimal W, decimal H);
    private sealed record PlacedPart(decimal X, decimal Y, decimal Width, decimal Height, bool Rotated);
}
