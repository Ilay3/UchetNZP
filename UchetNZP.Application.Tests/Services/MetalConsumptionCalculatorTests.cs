using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class MetalConsumptionCalculatorTests
{
    [Theory]
    [InlineData("Ø8x23", "rod", 8, null, null, 23, 10, 0.23)]
    [InlineData("15x25", "sheet", null, null, 15, 25, 10, 0.00375)]
    [InlineData("1,5x38x90", "plate", null, 1.5, 38, 90, 10, 0.0342)]
    public void Calculate_UsesExpectedGeometryForExamples(string _, string shape, decimal? diameterMm, decimal? thicknessMm, decimal? widthMm, decimal? lengthMm, decimal qty, decimal expectedNeedBase)
    {
        var norm = new MetalConsumptionNorm
        {
            ShapeType = shape,
            DiameterMm = diameterMm,
            ThicknessMm = thicknessMm,
            WidthMm = widthMm,
            LengthMm = lengthMm,
            BaseConsumptionQty = 1m,
            ConsumptionUnit = "m",
        };

        var material = new MetalMaterial
        {
            MassPerMeterKg = 2m,
            MassPerSquareMeterKg = 5m,
            CoefConsumption = 1.1m,
            StockUnit = "kg",
        };

        var result = MetalConsumptionCalculator.Calculate(norm, qty, material);

        if (shape == "rod")
        {
            Assert.Equal(expectedNeedBase, result.NeedM);
            Assert.True(result.NeedKg > 0m);
            return;
        }

        Assert.Equal(expectedNeedBase, result.NeedM2);
        Assert.True(result.NeedKg > 0m);
    }
}
