namespace UchetNZP.Domain.Entities;

public class Part
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public string Name { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public Guid SectionId { get; set; }

    public virtual Section? Section { get; set; }

    public virtual ICollection<PartRoute> Routes { get; set; } = new List<PartRoute>();

    public virtual ICollection<WipBalance> WipBalances { get; set; } = new List<WipBalance>();

    public virtual ICollection<WipReceipt> WipReceipts { get; set; } = new List<WipReceipt>();

    public virtual ICollection<WipLaunch> WipLaunches { get; set; } = new List<WipLaunch>();
}
