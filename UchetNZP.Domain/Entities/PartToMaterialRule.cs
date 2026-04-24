namespace UchetNZP.Domain.Entities;

public class PartToMaterialRule
{
    public Guid Id { get; set; }

    public string PartNamePattern { get; set; } = string.Empty;

    public string GeometryType { get; set; } = string.Empty;

    public string RolledType { get; set; } = string.Empty;

    public decimal? SizeFromMm { get; set; }

    public decimal? SizeToMm { get; set; }

    public string? MaterialGradePattern { get; set; }

    public string MaterialArticle { get; set; } = string.Empty;

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;
}
