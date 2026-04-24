using UchetNZP.Application.Services;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class MetalSizeParserTests
{
    public static IEnumerable<object[]> ParseCases()
    {
        yield return Case("Штырь", "Ø10x1200", "rod", "ok", 10m, null, null, 1200m, "pcs");
        yield return Case("Штырь", "Ø12х1500", "rod", "ok", 12m, null, null, 1500m, "pcs");
        yield return Case("Штырь", "Ø14*2000", "rod", "ok", 14m, null, null, 2000m, "pcs");
        yield return Case("Штырь", "ф16x2500", "rod", "ok", 16m, null, null, 2500m, "pcs");
        yield return Case("Штырь", "Ф20х3000", "rod", "ok", 20m, null, null, 3000m, "pcs");
        yield return Case("Штырь", "Ø18,5x1200", "rod", "ok", 18.5m, null, null, 1200m, "pcs");
        yield return Case("Штырь", "Ø22.25х600", "rod", "ok", 22.25m, null, null, 600m, "pcs");

        yield return Case("Бирка", "1x25x50", "plate", "ok", null, 1m, 25m, 50m, "pcs");
        yield return Case("Бирка", "1,5х30х80", "plate", "ok", null, 1.5m, 30m, 80m, "pcs");
        yield return Case("Бирка", "2.0*40*90", "plate", "ok", null, 2m, 40m, 90m, "pcs");
        yield return Case("Бирка", "0,8 x 20 x 35", "plate", "ok", null, 0.8m, 20m, 35m, "pcs");
        yield return Case("Бирка", "3х100х200", "plate", "ok", null, 3m, 100m, 200m, "pcs");
        yield return Case("Бирка", "4*120*240", "plate", "ok", null, 4m, 120m, 240m, "pcs");
        yield return Case("Бирка", "5.5х150х300", "plate", "ok", null, 5.5m, 150m, 300m, "pcs");

        yield return Case("Наконечник", "20x40", "sheet", "ok", null, null, 20m, 40m, "pcs");
        yield return Case("Наконечник", "25х50", "sheet", "ok", null, null, 25m, 50m, "pcs");
        yield return Case("Наконечник", "30*60", "sheet", "ok", null, null, 30m, 60m, "pcs");
        yield return Case("Наконечник", "12,5 x 55", "sheet", "ok", null, null, 12.5m, 55m, "pcs");
        yield return Case("Наконечник", "14.2х75", "sheet", "ok", null, null, 14.2m, 75m, "pcs");
        yield return Case("Наконечник", "80*120", "sheet", "ok", null, null, 80m, 120m, "pcs");

        yield return Case("Корпус", "5кг", "unknown", "ok", null, null, null, null, "kg", 5m);
        yield return Case("Корпус", "7,5 кг", "unknown", "ok", null, null, null, null, "kg", 7.5m);
        yield return Case("Корпус", "12.25kg", "unknown", "ok", null, null, null, null, "kg", 12.25m);
        yield return Case("Корпус", "10м", "unknown", "ok", null, null, null, null, "m", 10m);
        yield return Case("Корпус", "3,75 м", "unknown", "ok", null, null, null, null, "m", 3.75m);
        yield return Case("Корпус", "4.5m", "unknown", "ok", null, null, null, null, "m", 4.5m);
        yield return Case("Корпус", "2м2", "unknown", "ok", null, null, null, null, "m2", 2m);
        yield return Case("Корпус", "1,25 м2", "unknown", "ok", null, null, null, null, "m2", 1.25m);

        yield return Case("Сухарь", "74", "unknown", "partial", null, null, null, null, "pcs");
        yield return Case("Сухарь", "text", "unknown", "failed", null, null, null, null, "pcs");
        yield return Case("Сухарь", "", "unknown", "failed", null, null, null, null, "pcs");
        yield return Case("Сухарь", "Ø10", "unknown", "failed", null, null, null, null, "pcs");
        yield return Case("Сухарь", "10x", "unknown", "failed", null, null, null, null, "pcs");
        yield return Case("Сухарь", "x20", "unknown", "failed", null, null, null, null, "pcs");
        yield return Case("Сухарь", "1x2x3x4", "unknown", "failed", null, null, null, null, "pcs");
    }

    [Theory]
    [MemberData(nameof(ParseCases))]
    public void Parse_ExpectedResult(
        string _,
        string input,
        string expectedShape,
        string expectedStatus,
        decimal? diameter,
        decimal? thickness,
        decimal? width,
        decimal? length,
        string expectedUnit,
        decimal? expectedValue = null)
    {
        var result = MetalSizeParser.Parse(input, null, null);

        Assert.Equal(expectedShape, result.ShapeType);
        Assert.Equal(expectedStatus, result.ParseStatus);
        Assert.Equal(diameter, result.DiameterMm);
        Assert.Equal(thickness, result.ThicknessMm);
        Assert.Equal(width, result.WidthMm);
        Assert.Equal(length, result.LengthMm);
        Assert.Equal(expectedUnit, result.UnitNorm);
        Assert.Equal(expectedValue, result.ValueNorm);
    }

    private static object[] Case(
        string group,
        string input,
        string shape,
        string status,
        decimal? diameter,
        decimal? thickness,
        decimal? width,
        decimal? length,
        string unit,
        decimal? value = null)
    {
        return [group, input, shape, status, diameter, thickness, width, length, unit, value];
    }
}
