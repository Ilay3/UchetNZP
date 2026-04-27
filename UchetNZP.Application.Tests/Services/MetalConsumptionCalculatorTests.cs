using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class MetalConsumptionCalculatorTests
{
    [Fact]
    public void Calculate_ForMeters_UsesBaseConsumptionAsSourceOfTruth()
    {
        var norm = CreateNorm(2.5m, "m");
        var material = CreateMaterial(weightPerUnitKg: 0.00325m, coefficient: 1.2m);

        var result = MetalConsumptionCalculator.Calculate(norm, quantity: 4m, material);

        Assert.Equal(10m, result.NeedM);
        Assert.Equal(0m, result.NeedM2);
        Assert.Equal(0m, result.NeedPcs);
        Assert.Equal(0.039m, result.NeedKg);
        Assert.DoesNotContain("warning", result.Formula, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Calculate_ForSquareMeters_UsesBaseConsumptionAsSourceOfTruth()
    {
        var norm = CreateNorm(0.25m, "m2", shapeType: "sheet", widthMm: 150m, lengthMm: 300m);
        var material = CreateMaterial(weightPerUnitKg: 7m, coefficient: 1.05m);

        var result = MetalConsumptionCalculator.Calculate(norm, quantity: 8m, material);

        Assert.Equal(0m, result.NeedM);
        Assert.Equal(2m, result.NeedM2);
        Assert.Equal(0m, result.NeedPcs);
        Assert.Equal(14.7m, result.NeedKg);
    }

    [Fact]
    public void Calculate_ForKilograms_UsesBaseConsumptionDirectly()
    {
        var norm = CreateNorm(0.00325m, "kg");
        var material = CreateMaterial(weightPerUnitKg: 99m, coefficient: 1.1m);

        var result = MetalConsumptionCalculator.Calculate(norm, quantity: 10m, material);

        Assert.Equal(0m, result.NeedM);
        Assert.Equal(0m, result.NeedM2);
        Assert.Equal(0.0325m, result.NeedPcs);
        Assert.Equal(0.03575m, result.NeedKg);
    }

    [Fact]
    public void Calculate_ForGrams_ConvertsToKilogramsCorrectly()
    {
        var norm = CreateNorm(7m, "g");
        var material = CreateMaterial(weightPerUnitKg: 10m, coefficient: 1m);

        var result = MetalConsumptionCalculator.Calculate(norm, quantity: 12m, material);

        Assert.Equal(84m, result.NeedPcs);
        Assert.Equal(0.084m, result.NeedKg);
    }

    [Fact]
    public void Calculate_KeepsPrecisionForSmallValues()
    {
        var norm = CreateNorm(0.000084m, "kg");
        var material = CreateMaterial(weightPerUnitKg: 1m, coefficient: 1m);

        var result = MetalConsumptionCalculator.Calculate(norm, quantity: 1m, material);

        Assert.Equal(0.000084m, result.NeedKg);
    }

    [Fact]
    public void Calculate_WhenGeometryDiffersSignificantly_AddsWarningButKeepsBaseCalculation()
    {
        var norm = CreateNorm(1m, "m", shapeType: "rod", lengthMm: 200m);
        var material = CreateMaterial(weightPerUnitKg: 0.8m, coefficient: 1m);

        var result = MetalConsumptionCalculator.Calculate(norm, quantity: 5m, material);

        Assert.Equal(5m, result.NeedM);
        Assert.Equal(4m, result.NeedKg);
        Assert.Contains("warning", result.Formula, StringComparison.OrdinalIgnoreCase);
    }

    private static MetalConsumptionNorm CreateNorm(
        decimal baseConsumptionQty,
        string unit,
        string shapeType = "unknown",
        decimal? widthMm = null,
        decimal? lengthMm = null)
    {
        return new MetalConsumptionNorm
        {
            BaseConsumptionQty = baseConsumptionQty,
            ConsumptionUnit = unit,
            ShapeType = shapeType,
            WidthMm = widthMm,
            LengthMm = lengthMm,
        };
    }

    private static MetalMaterial CreateMaterial(decimal weightPerUnitKg, decimal coefficient)
    {
        return new MetalMaterial
        {
            WeightPerUnitKg = weightPerUnitKg,
            Coefficient = coefficient,
            CoefConsumption = 1m,
            MassPerMeterKg = 0.8m,
            MassPerSquareMeterKg = 7m,
            StockUnit = "kg",
        };
    }
}
