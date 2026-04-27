using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Abstractions;

public interface IMaterialSelectionService
{
    MaterialSelectionDecision ResolveForNorm(
        string? partName,
        MetalConsumptionNorm? norm,
        IReadOnlyCollection<PartToMaterialRule> rules,
        IReadOnlyCollection<MetalMaterial> activeMaterials);
}

public sealed record MaterialSelectionDecision(
    bool IsResolved,
    Guid? MetalMaterialId,
    string Source,
    string Reason,
    string? CandidatesDisplay,
    string SelectionStatus)
{
    public static MaterialSelectionDecision Resolved(Guid materialId, string source, string reason, IReadOnlyCollection<string> candidates)
    {
        var candidateString = candidates.Count == 0 ? null : string.Join("; ", candidates);
        return new MaterialSelectionDecision(true, materialId, source, reason, candidateString, "Resolved");
    }

    public static MaterialSelectionDecision NeedSelection(string reason, IReadOnlyCollection<string>? candidates = null)
    {
        var candidateString = candidates is { Count: > 0 } ? string.Join("; ", candidates) : null;
        return new MaterialSelectionDecision(false, null, "manual", reason, candidateString, "NeedMaterialSelection");
    }
}
