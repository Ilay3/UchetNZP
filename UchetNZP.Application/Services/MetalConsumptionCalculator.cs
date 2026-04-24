using System.Text.Json;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Services;

public static class MetalConsumptionCalculator
{
    public static MetalConsumptionCalculationResult Calculate(MetalConsumptionNorm norm, decimal quantity, MetalMaterial material)
    {
        var normalizedShape = (norm.ShapeType ?? string.Empty).Trim().ToLowerInvariant();

        var needM = 0m;
        var needM2 = 0m;
        var needPcs = 0m;
        string formula;

        if (normalizedShape == "rod" && norm.LengthMm.HasValue)
        {
            needM = quantity * (norm.LengthMm.Value / 1000m);
            formula = "need_m = qty * length_m";
        }
        else if ((normalizedShape == "sheet" || normalizedShape == "plate") && norm.WidthMm.HasValue && norm.LengthMm.HasValue)
        {
            var areaM2 = (norm.WidthMm.Value * norm.LengthMm.Value) / 1_000_000m;
            needM2 = quantity * areaM2;
            formula = "need_m2 = qty * area_m2";
        }
        else
        {
            var unit = (norm.ConsumptionUnit ?? string.Empty).Trim().ToLowerInvariant();
            if (unit is "м" or "m")
            {
                needM = norm.BaseConsumptionQty * quantity;
                formula = "need_m = qty * base_consumption_qty";
            }
            else if (unit is "м2" or "m2" or "м²")
            {
                needM2 = norm.BaseConsumptionQty * quantity;
                formula = "need_m2 = qty * base_consumption_qty";
            }
            else
            {
                needPcs = norm.BaseConsumptionQty * quantity;
                formula = "need_pcs = qty * base_consumption_qty";
            }
        }

        var needKg = (needM * material.MassPerMeterKg + needM2 * material.MassPerSquareMeterKg) * material.CoefConsumption;

        var metersFromKg = material.MassPerMeterKg > 0m
            ? needKg / (material.MassPerMeterKg * material.CoefConsumption)
            : 0m;
        var squareMetersFromKg = material.MassPerSquareMeterKg > 0m
            ? needKg / (material.MassPerSquareMeterKg * material.CoefConsumption)
            : 0m;

        var formulaWithMass =
            "need_kg = (need_m * mass_per_meter + need_m2 * mass_per_m2) * coef_consumption";

        var input = JsonSerializer.Serialize(new
        {
            qty = quantity,
            length_m = norm.LengthMm.HasValue ? norm.LengthMm.Value / 1000m : (decimal?)null,
            area_m2 = norm.WidthMm.HasValue && norm.LengthMm.HasValue ? (norm.WidthMm.Value * norm.LengthMm.Value) / 1_000_000m : (decimal?)null,
            mass_per_meter = material.MassPerMeterKg,
            mass_per_m2 = material.MassPerSquareMeterKg,
            coef_consumption = material.CoefConsumption,
            stock_unit = material.StockUnit,
        });

        return new MetalConsumptionCalculationResult(
            NeedM: needM,
            NeedM2: needM2,
            NeedPcs: needPcs,
            NeedKg: needKg,
            MetersFromKg: metersFromKg,
            SquareMetersFromKg: squareMetersFromKg,
            Formula: $"{formula}; {formulaWithMass}",
            FormulaInput: input);
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
