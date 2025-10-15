namespace UchetNZP.Domain.Entities;

public class WipLaunchOperation
{
    public Guid Id { get; set; }

    public Guid WipLaunchId { get; set; }

    public Guid OperationId { get; set; }

    public Guid SectionId { get; set; }

    public int OpNumber { get; set; }

    public Guid? PartRouteId { get; set; }

    public decimal Quantity { get; set; }

    public decimal Hours { get; set; }

    public decimal NormHours { get; set; }

    public virtual WipLaunch? WipLaunch { get; set; }

    public virtual Operation? Operation { get; set; }

    public virtual Section? Section { get; set; }

    public virtual PartRoute? PartRoute { get; set; }
}
