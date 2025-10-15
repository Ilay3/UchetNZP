using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class ImportJob
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public string Status { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; } // Ограничение длины -> Fluent API

    public virtual ICollection<ImportJobItem> Items { get; set; } = new List<ImportJobItem>();
}
