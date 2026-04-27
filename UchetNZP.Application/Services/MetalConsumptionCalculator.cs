using System.Text.Json;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Services;

public static class MetalConsumptionCalculator
{
    private const decimal GeometryMismatchRelativeThreshold = 0.05m;

    public static MetalConsumptionCalculationResult Calculate(MetalConsumptionNorm norm, decimal quantity, MetalMaterial material)
    {
        var unit = NormalizeUnit(norm.ConsumptionUnit);
        var coefficient = ResolveCoefficient(material);
        var weightPerUnitKg = ResolveWeightPerUnitKg(material, unit);

        var needM = 0m;
        var needM2 = 0m;
        var needPcs = 0m;
        var needKg = 0m;

        string formula;

        switch (unit)
        {
            case "m":
                needM = norm.BaseConsumptionQty * quantity;
                needKg = needM * weightPerUnitKg * coefficient;
                formula = "need_m = qty * base_consumption_qty; need_kg = need_m * weight_per_unit_kg * coefficient";
                break;
            case "m2":
                needM2 = norm.BaseConsumptionQty * quantity;
                needKg = needM2 * weightPerUnitKg * coefficient;
                formula = "need_m2 = qty * base_consumption_qty; need_kg = need_m2 * weight_per_unit_kg * coefficient";
                break;
            case "kg":
                needPcs = norm.BaseConsumptionQty * quantity;
                needKg = needPcs * coefficient;
                formula = "need_kg = qty * base_consumption_qty * coefficient";
                break;
            case "g":
                needPcs = norm.BaseConsumptionQty * quantity;
                needKg = quantity * (norm.BaseConsumptionQty / 1000m) * coefficient;
                formula = "need_kg = qty * (base_consumption_qty / 1000) * coefficient";
                break;
            default:
                needPcs = norm.BaseConsumptionQty * quantity;
                needKg = needPcs * coefficient;
                formula = "need_qty = qty * base_consumption_qty; need_kg = need_qty * coefficient";
                break;
        }

        var metersFromKg = weightPerUnitKg > 0m && unit == "m"
            ? needKg / (weightPerUnitKg * coefficient)
            : 0m;
        var squareMetersFromKg = weightPerUnitKg > 0m && unit == "m2"
            ? needKg / (weightPerUnitKg * coefficient)
            : 0m;

        var geometryDerivedQty = TryCalculateGeometryDerivedQty(norm);
        var (hasMismatchWarning, mismatchDetails) = EvaluateMismatch(geometryDerivedQty, norm.BaseConsumptionQty);

        var input = JsonSerializer.Serialize(new
        {
            qty = quantity,
            base_consumption_qty = norm.BaseConsumptionQty,
            consumption_unit = unit,
            weight_per_unit_kg = weightPerUnitKg,
            coefficient,
            size_raw = norm.SizeRaw,
            geometry_derived_qty = geometryDerivedQty,
            has_geometry_mismatch_warning = hasMismatchWarning,
            geometry_mismatch_details = mismatchDetails,
        });

        if (hasMismatchWarning)
        {
            formula += "; warning = geometry_derived_qty differs from base_consumption_qty";
        }

        return new MetalConsumptionCalculationResult(
            NeedM: needM,
            NeedM2: needM2,
            NeedPcs: needPcs,
            NeedKg: needKg,
            MetersFromKg: metersFromKg,
            SquareMetersFromKg: squareMetersFromKg,
            Formula: formula,
            FormulaInput: input);
    }

    private static string NormalizeUnit(string? unit)
    {
        var normalized = (unit ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "м" => "m",
            "м2" or "м²" => "m2",
            _ => normalized,
        };
    }

    private static decimal ResolveCoefficient(MetalMaterial material)
    {
        if (material.Coefficient > 0m)
        {
            return material.Coefficient;
        }

        if (material.CoefConsumption > 0m)
        {
            return material.CoefConsumption;
        }

        return 1m;
    }

    private static decimal ResolveWeightPerUnitKg(MetalMaterial material, string unit)
    {
        if (material.WeightPerUnitKg.HasValue && material.WeightPerUnitKg.Value > 0m)
        {
            return material.WeightPerUnitKg.Value;
        }

        return unit switch
        {
            "m" when material.MassPerMeterKg > 0m => material.MassPerMeterKg,
            "m2" when material.MassPerSquareMeterKg > 0m => material.MassPerSquareMeterKg,
            _ => 0m,
        };
    }

    private static decimal? TryCalculateGeometryDerivedQty(MetalConsumptionNorm norm)
    {
        var shape = (norm.ShapeType ?? string.Empty).Trim().ToLowerInvariant();

        if (shape == "rod" && norm.LengthMm.HasValue)
        {
            return norm.LengthMm.Value / 1000m;
        }

        if ((shape == "sheet" || shape == "plate") && norm.WidthMm.HasValue && norm.LengthMm.HasValue)
        {
            return (norm.WidthMm.Value * norm.LengthMm.Value) / 1_000_000m;
        }

        return null;
    }

    private static (bool HasMismatchWarning, string? Details) EvaluateMismatch(decimal? geometryDerivedQty, decimal baseConsumptionQty)
    {
        if (!geometryDerivedQty.HasValue)
        {
            return (false, null);
        }

        var diff = Math.Abs(geometryDerivedQty.Value - baseConsumptionQty);
        if (diff == 0m)
        {
            return (false, null);
        }

        var denominator = Math.Abs(baseConsumptionQty) > 0m ? Math.Abs(baseConsumptionQty) : 1m;
        var relativeDiff = diff / denominator;

        if (relativeDiff <= GeometryMismatchRelativeThreshold)
        {
            return (false, null);
        }

        return (true, $"geometry={geometryDerivedQty.Value:0.######}; base={baseConsumptionQty:0.######}; relative_diff={relativeDiff:P2}");
    }
}

public sealed record MetalConsumptionCalculationResult(
    decimal NeedM,
    decimal NeedM2,
    decimal NeedPcs,
    decimal NeedKg,
    decimal MetersFromKg,
    decimal SquareMetersFromKg,
    string Formula,
    string FormulaInput);
