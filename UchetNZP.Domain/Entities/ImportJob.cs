using System.Collections.Generic;

namespace UchetNZP.Domain.Entities;

public class ImportJob
{
    public Guid Id { get; set; }

    public DateTime Ts { get; set; }

    public Guid UserId { get; set; }

    public string FileName { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public int TotalRows { get; set; }

    public int Succeeded { get; set; }

    public int Skipped { get; set; }

    public string? ErrorMessage { get; set; } // Ограничение длины -> Fluent API

    public virtual ICollection<ImportJobItem> Items { get; set; } = new List<ImportJobItem>();
}
