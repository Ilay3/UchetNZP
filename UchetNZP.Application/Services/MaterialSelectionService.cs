using UchetNZP.Application.Abstractions;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Services;

public class MaterialSelectionService : IMaterialSelectionService
{
    public MaterialSelectionDecision ResolveForNorm(
        string? partName,
        MetalConsumptionNorm? norm,
        IReadOnlyCollection<PartToMaterialRule> rules,
        IReadOnlyCollection<MetalMaterial> activeMaterials)
    {
        if (norm is null)
        {
            return MaterialSelectionDecision.NeedSelection("Не найдена активная норма расхода для детали.");
        }

        if (norm.MetalMaterialId.HasValue && norm.MetalMaterialId.Value != Guid.Empty)
        {
            var normMaterial = activeMaterials.FirstOrDefault(x => x.Id == norm.MetalMaterialId.Value);
            if (normMaterial is not null)
            {
                return MaterialSelectionDecision.Resolved(
                    normMaterial.Id,
                    "norm",
                    "Материал взят из MetalMaterialId нормы расхода.",
                    []);
            }
        }

        var resolvedByRule = TryResolveByRules(partName, norm, rules, activeMaterials);
        if (resolvedByRule is not null)
        {
            return resolvedByRule;
        }

        return ResolveByFallback(partName, norm, activeMaterials);
    }

    private static MaterialSelectionDecision? TryResolveByRules(
        string? partName,
        MetalConsumptionNorm norm,
        IReadOnlyCollection<PartToMaterialRule> rules,
        IReadOnlyCollection<MetalMaterial> activeMaterials)
    {
        var normalizedPartName = partName ?? string.Empty;

        var candidates = rules
            .Where(rule => rule.IsActive)
            .Where(rule => IsPatternMatch(normalizedPartName, rule.PartNamePattern))
            .Where(rule => string.Equals(rule.GeometryType, norm.ShapeType, StringComparison.OrdinalIgnoreCase))
            .Where(rule => IsRuleSizeMatch(rule, norm))
            .OrderByDescending(rule => rule.Priority)
            .Select(rule => new
            {
                Rule = rule,
                Material = activeMaterials.FirstOrDefault(m =>
                    string.Equals(m.Code, rule.MaterialArticle, StringComparison.OrdinalIgnoreCase)
                    || m.Name.Contains(rule.MaterialArticle, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.Material is not null)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var winner = candidates[0];
        var candidateTexts = candidates
            .Take(3)
            .Select(x => $"{x.Material!.Name} ({x.Material.Code ?? "без артикула"}): правило #{x.Rule.Priority}")
            .ToList();

        return MaterialSelectionDecision.Resolved(
            winner.Material!.Id,
            "auto_rule",
            $"Подобрано по правилу PartToMaterialRule с приоритетом {winner.Rule.Priority}.",
            candidateTexts);
    }

    private static MaterialSelectionDecision ResolveByFallback(
        string? partName,
        MetalConsumptionNorm norm,
        IReadOnlyCollection<MetalMaterial> activeMaterials)
    {
        var rolledType = DetectRolledType(norm, partName ?? string.Empty);

        var fallbackCandidates = activeMaterials
            .Select(material => new
            {
                Material = material,
                Score = CalculateFallbackScore(material, rolledType, norm),
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Material.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (fallbackCandidates.Count == 0)
        {
            return MaterialSelectionDecision.NeedSelection("Не удалось автоматически подобрать материал. Выберите материал вручную.");
        }

        var topScore = fallbackCandidates[0].Score;
        var ambiguous = fallbackCandidates.Count > 1 && fallbackCandidates[1].Score == topScore;
        var fallbackTexts = fallbackCandidates
            .Take(3)
            .Select(x => $"{x.Material.Name} ({x.Material.Code ?? "без артикула"}): score {x.Score}")
            .ToList();

        if (ambiguous)
        {
            return MaterialSelectionDecision.NeedSelection(
                $"Найдено несколько равнозначных кандидатов ({string.Join("; ", fallbackTexts)}). Укажите материал вручную.",
                fallbackTexts);
        }

        return MaterialSelectionDecision.Resolved(
            fallbackCandidates[0].Material.Id,
            "fallback",
            $"Материал подобран по fallback-эвристике для типа проката '{rolledType}'.",
            fallbackTexts);
    }

    private static bool IsPatternMatch(string partName, string pattern)
    {
        var normalizedPattern = (pattern ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedPattern))
        {
            return true;
        }

        return partName.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuleSizeMatch(PartToMaterialRule rule, MetalConsumptionNorm norm)
    {
        var size = norm.DiameterMm ?? norm.ThicknessMm ?? norm.WidthMm;
        if (!size.HasValue)
        {
            return true;
        }

        var from = rule.SizeFromMm ?? decimal.MinValue;
        var to = rule.SizeToMm ?? decimal.MaxValue;
        return size.Value >= from && size.Value <= to;
    }

    private static string DetectRolledType(MetalConsumptionNorm norm, string partName)
    {
        if (string.Equals(norm.ShapeType, "rod", StringComparison.OrdinalIgnoreCase) || partName.Contains("штыр", StringComparison.OrdinalIgnoreCase))
        {
            return "rod";
        }

        if (string.Equals(norm.ShapeType, "sheet", StringComparison.OrdinalIgnoreCase) || partName.Contains("бирк", StringComparison.OrdinalIgnoreCase))
        {
            return "sheet";
        }

        return norm.ShapeType switch
        {
            "pipe" => "pipe",
            _ => "sheet",
        };
    }

    private static int CalculateFallbackScore(MetalMaterial material, string rolledType, MetalConsumptionNorm norm)
    {
        var score = 0;
        var haystack = $"{material.Name} {material.Code}".ToLowerInvariant();

        if (rolledType == "rod" && (haystack.Contains("круг") || haystack.Contains("прут")))
        {
            score += 10;
        }
        else if (rolledType == "sheet" && haystack.Contains("лист"))
        {
            score += 10;
        }
        else if (rolledType == "pipe" && haystack.Contains("труб"))
        {
            score += 10;
        }

        var target = norm.DiameterMm ?? norm.ThicknessMm ?? norm.WidthMm;
        if (target.HasValue)
        {
            var marker = target.Value.ToString("0.###").Replace(',', '.');
            if (haystack.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }
        }

        return score;
    }
}
