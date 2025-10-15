namespace UchetNZP.Domain.Entities;

public class Operation
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public virtual ICollection<PartRoute> PartRoutes { get; set; } = new List<PartRoute>();

    public virtual ICollection<WipLaunchOperation> WipLaunchOperations { get; set; } = new List<WipLaunchOperation>();
}
