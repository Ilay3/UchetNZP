namespace UchetNZP.Domain.Entities;

public class Section
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public string Name { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public virtual ICollection<Part> Parts { get; set; } = new List<Part>();

    public virtual ICollection<WipBalance> WipBalances { get; set; } = new List<WipBalance>();

    public virtual ICollection<WipReceipt> WipReceipts { get; set; } = new List<WipReceipt>();

    public virtual ICollection<WipLaunch> WipLaunches { get; set; } = new List<WipLaunch>();
}
