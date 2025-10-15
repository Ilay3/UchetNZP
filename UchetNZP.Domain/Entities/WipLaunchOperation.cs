namespace UchetNZP.Domain.Entities;

public class WipLaunchOperation
{
    public Guid Id { get; set; }

    public Guid WipLaunchId { get; set; }

    public Guid OperationId { get; set; }

    public Guid? PartRouteId { get; set; }

    public int Sequence { get; set; }

    public decimal Quantity { get; set; }

    public decimal? CompletedQuantity { get; set; }

    public virtual WipLaunch? WipLaunch { get; set; }

    public virtual Operation? Operation { get; set; }

    public virtual PartRoute? PartRoute { get; set; }
}
