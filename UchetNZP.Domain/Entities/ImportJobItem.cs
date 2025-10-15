namespace UchetNZP.Domain.Entities;

public class ImportJobItem
{
    public Guid Id { get; set; }

    public Guid ImportJobId { get; set; }

    public int RowIndex { get; set; }

    public string Status { get; set; } = string.Empty; // Ограничение длины -> Fluent API

    public string? Message { get; set; } // Ограничение длины -> Fluent API

    public virtual ImportJob? ImportJob { get; set; }
}
