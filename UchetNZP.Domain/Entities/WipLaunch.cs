using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class WipLaunch
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PartId { get; set; }

    public Guid SectionId { get; set; }

    public int FromOpNumber { get; set; }

    public DateTime LaunchDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal Quantity { get; set; }

    public string? DocumentNumber { get; set; }

    public string? Comment { get; set; }

    public decimal SumHoursToFinish { get; set; }

    public virtual Part? Part { get; set; }

    public virtual Section? Section { get; set; }

    public virtual ICollection<WipLaunchOperation> Operations { get; set; } = new List<WipLaunchOperation>();
}
