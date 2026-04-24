using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalIssue
{
    public Guid Id { get; set; }

    public Guid MetalRequirementId { get; set; }

    public string IssueNumber { get; set; } = string.Empty;

    public DateTime IssueDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? CompletedAt { get; set; }

    public string? CompletedBy { get; set; }

    public virtual MetalRequirement? MetalRequirement { get; set; }

    public virtual ICollection<MetalIssueItem> Items { get; set; } = new List<MetalIssueItem>();
}
