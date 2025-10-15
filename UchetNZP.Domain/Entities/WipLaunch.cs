namespace UchetNZP.Domain.Entities;

public class WipLaunch
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public Guid SectionId { get; set; }

    public DateTime LaunchDate { get; set; }

    public decimal Quantity { get; set; }

    public string? DocumentNumber { get; set; }

    public virtual Part? Part { get; set; }

    public virtual Section? Section { get; set; }

    public virtual ICollection<WipLaunchOperation> Operations { get; set; } = new List<WipLaunchOperation>();
}
