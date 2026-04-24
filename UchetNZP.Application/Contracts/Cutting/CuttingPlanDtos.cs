namespace UchetNZP.Application.Contracts.Cutting;

public sealed record LinearCutRequest(
    decimal StockLength,
    decimal Kerf,
    decimal MinBusinessResidual,
    IReadOnlyList<LinearCutPartRequest> Parts);

public sealed record LinearCutPartRequest(decimal Length, int Quantity);

public sealed record SheetCutRequest(
    decimal StockWidth,
    decimal StockHeight,
    decimal Margin,
    decimal Gap,
    IReadOnlyList<SheetCutPartRequest> Parts,
    bool AllowRotation);

public sealed record SheetCutPartRequest(decimal Width, decimal Height, int Quantity);

public sealed record SaveCuttingPlanRequest(
    Guid MetalRequirementId,
    LinearCutRequest? Linear,
    SheetCutRequest? Sheet);

public sealed record CuttingPlanResultDto(
    Guid PlanId,
    int Version,
    string Kind,
    decimal UtilizationPercent,
    decimal WastePercent,
    int CutCount,
    decimal BusinessResidual,
    decimal ScrapResidual,
    IReadOnlyList<CuttingPlanStockDto> Stocks);

public sealed record CuttingPlanStockDto(int StockIndex, IReadOnlyList<CuttingPlanPlacementDto> Placements);

public sealed record CuttingPlanPlacementDto(
    string ItemType,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    decimal? PositionX,
    decimal? PositionY,
    bool Rotated,
    int Quantity);
