namespace UchetNZP.Domain.Entities;

public class PartRoute
{
    public Guid Id { get; set; }

    public Guid PartId { get; set; }

    public int OpNumber { get; set; }

    public Guid OperationId { get; set; }

    public Guid SectionId { get; set; }

    public decimal NormHours { get; set; }

    public virtual Part? Part { get; set; }

    public virtual Operation? Operation { get; set; }

    public virtual Section? Section { get; set; }

    public virtual ICollection<WipLaunchOperation> WipLaunchOperations { get; set; } = new List<WipLaunchOperation>();
}
