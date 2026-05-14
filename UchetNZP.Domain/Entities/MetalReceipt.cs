using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalReceipt
{
    public Guid Id { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }

    public string OrganizationName { get; set; } = "ООО Промавтоматика";

    public string WarehouseName { get; set; } = "Склад металла";

    public string OperationType { get; set; } = "Поступление товаров";

    public string CurrencyCode { get; set; } = "RUB";

    public string? ContractName { get; set; }

    public string? SupplierOrSource { get; set; }

    public Guid? MetalSupplierId { get; set; }

    public string? SupplierIdentifierSnapshot { get; set; }

    public string? SupplierNameSnapshot { get; set; }

    public string? SupplierInnSnapshot { get; set; }

    public string? SupplierDocumentNumber { get; set; }
    public DateTime? SupplierDocumentDate { get; set; }
    public string? InvoiceOrUpiNumber { get; set; }
    public string AccountingAccount { get; set; } = "10.01";
    public string VatAccount { get; set; } = "19.03";

    public string SettlementAccount { get; set; } = "60.01";

    public string AdvanceAccount { get; set; } = "60.02";

    public string? ResponsibleUserName { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public decimal PricePerKg { get; set; }

    public decimal AmountWithoutVat { get; set; }

    public decimal VatRatePercent { get; set; }

    public decimal VatAmount { get; set; }

    public decimal TotalAmountWithVat { get; set; }

    public string? OriginalDocumentFileName { get; set; }

    public string? OriginalDocumentContentType { get; set; }

    public byte[]? OriginalDocumentContent { get; set; }

    public long? OriginalDocumentSizeBytes { get; set; }

    public DateTime? OriginalDocumentUploadedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<MetalReceiptItem> Items { get; set; } = new List<MetalReceiptItem>();

    public virtual MetalSupplier? MetalSupplier { get; set; }
}
