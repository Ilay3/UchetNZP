using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class WarehouseAssemblyUnit
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<WarehouseItem> WarehouseItems { get; set; } = new List<WarehouseItem>();
}
