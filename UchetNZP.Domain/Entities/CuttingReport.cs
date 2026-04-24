namespace UchetNZP.Domain.Entities;

public class CuttingReport
{
    public Guid Id { get; set; }

    public string ReportNumber { get; set; } = string.Empty;

    public DateTime ReportDate { get; set; }

    public Guid CuttingPlanId { get; set; }

    public Guid SourceMetalReceiptItemId { get; set; }

    public string Workshop { get; set; } = string.Empty;

    public string Shift { get; set; } = string.Empty;

    public decimal PlannedSize { get; set; }

    public decimal ActualProducedSize { get; set; }

    public decimal PlannedMassKg { get; set; }

    public decimal ActualProducedMassKg { get; set; }

    public decimal PlannedWaste { get; set; }

    public decimal ActualWaste { get; set; }

    public decimal BusinessResidual { get; set; }

    public decimal ScrapSize { get; set; }

    public decimal ScrapMassKg { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual CuttingPlan? CuttingPlan { get; set; }

    public virtual MetalReceiptItem? SourceMetalReceiptItem { get; set; }
}
