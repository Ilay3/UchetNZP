namespace UchetNZP.Domain.Entities;

public class CuttingPlanItem
{
    public Guid Id { get; set; }

    public Guid CuttingPlanId { get; set; }

    public int StockIndex { get; set; }

    public int Sequence { get; set; }

    public string ItemType { get; set; } = string.Empty;

    public decimal? Length { get; set; }

    public decimal? Width { get; set; }

    public decimal? Height { get; set; }

    public decimal? PositionX { get; set; }

    public decimal? PositionY { get; set; }

    public bool Rotated { get; set; }

    public int Quantity { get; set; }

    public virtual CuttingPlan? CuttingPlan { get; set; }
}
