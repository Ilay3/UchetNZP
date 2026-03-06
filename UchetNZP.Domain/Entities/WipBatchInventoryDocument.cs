namespace UchetNZP.Domain.Entities;

public class WipBatchInventoryDocument
{
    public Guid Id { get; set; }

    public int InventoryNumber { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime ComposedAt { get; set; }

    public DateTime PeriodFrom { get; set; }

    public DateTime PeriodTo { get; set; }

    public string? PartFilter { get; set; }

    public string? SectionFilter { get; set; }

    public string? OpNumberFilter { get; set; }

    public int RowCount { get; set; }

    public decimal TotalQuantity { get; set; }

    public string FileName { get; set; } = string.Empty;

    public byte[] Content { get; set; } = Array.Empty<byte>();
}
