namespace UchetNZP.Domain.Entities;

public class ImportJobItem
{
    public Guid Id { get; set; }

    public Guid ImportJobId { get; set; }

    public string ExternalId { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public string Status { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public string? Payload { get; set; } // Ограничение длины -> Fluent API

    public string? ErrorMessage { get; set; } // Ограничение длины -> Fluent API

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public virtual ImportJob? ImportJob { get; set; }
}
