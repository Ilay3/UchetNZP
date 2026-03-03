namespace UchetNZP.Domain.Entities;

public class LabelNumberCounter
{
    public string RootNumber { get; set; } = string.Empty;

    public int NextSuffix { get; set; }
}
