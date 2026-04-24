using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class CuttingPlan
{
    public Guid Id { get; set; }

    public Guid MetalRequirementId { get; set; }

    public CuttingPlanKind Kind { get; set; }

    public int Version { get; set; }

    public string InputHash { get; set; } = string.Empty;

    public string ParametersJson { get; set; } = string.Empty;

    public decimal UtilizationPercent { get; set; }

    public decimal WastePercent { get; set; }

    public int CutCount { get; set; }

    public decimal BusinessResidual { get; set; }

    public decimal ScrapResidual { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsCurrent { get; set; }

    public string ExecutionStatus { get; set; } = "Не выполнено";

    public decimal? ActualResidual { get; set; }

    public virtual MetalRequirement? MetalRequirement { get; set; }

    public virtual ICollection<CuttingPlanItem> Items { get; set; } = new List<CuttingPlanItem>();
}
