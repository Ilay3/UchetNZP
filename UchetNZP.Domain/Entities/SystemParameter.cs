namespace UchetNZP.Domain.Entities;

public class SystemParameter
{
    public string Key { get; set; } = string.Empty;

    public decimal? DecimalValue { get; set; }

    public string? TextValue { get; set; }

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; }
}
