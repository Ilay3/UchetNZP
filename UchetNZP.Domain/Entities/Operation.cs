namespace UchetNZP.Domain.Entities;

public class Operation
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public string Name { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public virtual ICollection<PartRoute> PartRoutes { get; set; } = new List<PartRoute>();

    public virtual ICollection<WipLaunchOperation> WipLaunchOperations { get; set; } = new List<WipLaunchOperation>();
}
