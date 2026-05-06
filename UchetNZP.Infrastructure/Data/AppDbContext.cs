using Microsoft.EntityFrameworkCore;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Part> Parts => Set<Part>();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<Operation> Operations => Set<Operation>();

    public DbSet<PartRoute> PartRoutes => Set<PartRoute>();

    public DbSet<WipBalance> WipBalances => Set<WipBalance>();

    public DbSet<WipReceipt> WipReceipts => Set<WipReceipt>();

    public DbSet<WipLaunch> WipLaunches => Set<WipLaunch>();

    public DbSet<WipLaunchOperation> WipLaunchOperations => Set<WipLaunchOperation>();

    public DbSet<WipTransfer> WipTransfers => Set<WipTransfer>();

    public DbSet<WipTransferOperation> WipTransferOperations => Set<WipTransferOperation>();

    public DbSet<WipScrap> WipScraps => Set<WipScrap>();

    public DbSet<WipBalanceAdjustment> WipBalanceAdjustments => Set<WipBalanceAdjustment>();

    public DbSet<ReceiptAudit> ReceiptAudits => Set<ReceiptAudit>();

    public DbSet<WarehouseItem> WarehouseItems => Set<WarehouseItem>();

    public DbSet<WarehouseLabelItem> WarehouseLabelItems => Set<WarehouseLabelItem>();

    public DbSet<TransferAudit> TransferAudits => Set<TransferAudit>();

    public DbSet<TransferAuditOperation> TransferAuditOperations => Set<TransferAuditOperation>();

    public DbSet<TransferLabelUsage> TransferLabelUsages => Set<TransferLabelUsage>();

    public DbSet<LabelMerge> LabelMerges => Set<LabelMerge>();

    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    public DbSet<ImportJobItem> ImportJobItems => Set<ImportJobItem>();

    public DbSet<WipLabel> WipLabels => Set<WipLabel>();

    public DbSet<WipLabelLedger> WipLabelLedger => Set<WipLabelLedger>();

    public DbSet<LabelNumberCounter> LabelNumberCounters => Set<LabelNumberCounter>();

    public DbSet<WipBatchInventoryDocument> WipBatchInventoryDocuments => Set<WipBatchInventoryDocument>();

    public DbSet<MetalMaterial> MetalMaterials => Set<MetalMaterial>();

    public DbSet<MetalSupplier> MetalSuppliers => Set<MetalSupplier>();

    public DbSet<MetalReceipt> MetalReceipts => Set<MetalReceipt>();

    public DbSet<MetalReceiptItem> MetalReceiptItems => Set<MetalReceiptItem>();

    public DbSet<MetalConsumptionNorm> MetalConsumptionNorms => Set<MetalConsumptionNorm>();

    public DbSet<MetalRequirement> MetalRequirements => Set<MetalRequirement>();

    public DbSet<MetalRequirementItem> MetalRequirementItems => Set<MetalRequirementItem>();

    public DbSet<MetalRequirementPlan> MetalRequirementPlans => Set<MetalRequirementPlan>();

    public DbSet<MetalRequirementPlanItem> MetalRequirementPlanItems => Set<MetalRequirementPlanItem>();

    public DbSet<MetalIssue> MetalIssues => Set<MetalIssue>();

    public DbSet<MetalIssueItem> MetalIssueItems => Set<MetalIssueItem>();

    public DbSet<MetalStockMovement> MetalStockMovements => Set<MetalStockMovement>();
    public DbSet<MetalAuditLog> MetalAuditLogs => Set<MetalAuditLog>();

    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();

    public DbSet<PartToMaterialRule> PartToMaterialRules => Set<PartToMaterialRule>();

    public DbSet<CuttingPlan> CuttingPlans => Set<CuttingPlan>();

    public DbSet<CuttingPlanItem> CuttingPlanItems => Set<CuttingPlanItem>();

    public DbSet<CuttingReport> CuttingReports => Set<CuttingReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
