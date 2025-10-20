using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class Part
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public virtual ICollection<PartRoute> Routes { get; set; } = new List<PartRoute>();

    public virtual ICollection<WipBalance> WipBalances { get; set; } = new List<WipBalance>();

    public virtual ICollection<WipReceipt> WipReceipts { get; set; } = new List<WipReceipt>();

    public virtual ICollection<WipLaunch> WipLaunches { get; set; } = new List<WipLaunch>();

    public virtual ICollection<WipScrap> WipScraps { get; set; } = new List<WipScrap>();
}
