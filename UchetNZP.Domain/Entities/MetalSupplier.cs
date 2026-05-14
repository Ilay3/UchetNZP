using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class MetalSupplier
{
    public Guid Id { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string Inn { get; set; } = string.Empty;

    public string? Kpp { get; set; }

    public string LegalEntityKind { get; set; } = "ЮридическоеЛицо";

    public string? CountryOfRegistration { get; set; }

    public string? Okpo { get; set; }

    public string? MainBankAccount { get; set; }

    public string? MainContractName { get; set; }

    public string? ContactPerson { get; set; }

    public string? AdditionalInfo { get; set; }

    public string? Comment { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<MetalReceipt> Receipts { get; set; } = new List<MetalReceipt>();
}
