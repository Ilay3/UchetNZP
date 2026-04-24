using System.Globalization;
using System.Text.RegularExpressions;

namespace UchetNZP.Application.Services;

public static class MetalSizeParser
{
    private static readonly Regex DelimiterRegex = new("\\s*[xх*]\\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex UnitValueRegex = new(
        "^(?<value>\\d+(?:[\\.,]\\d+)?)\\s*(?<unit>kg|кг|m2|м2|m|м|pcs|шт)\\.?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static MetalSizeParseResult Parse(string? rawValue, string? unitRaw, decimal? valueRaw)
    {
        var raw = rawValue?.Trim() ?? string.Empty;
        var normalizedUnit = NormalizeUnit(unitRaw);
        var normalizedValue = valueRaw;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return Failed(raw, normalizedUnit, normalizedValue, "Пустое поле размера.");
        }

        var unitValue = TryParseUnitValue(raw);
        if (unitValue is not null)
        {
            return new MetalSizeParseResult(
                ShapeType: "unknown",
                DiameterMm: null,
                ThicknessMm: null,
                WidthMm: null,
                LengthMm: null,
                UnitNorm: unitValue.Value.UnitNorm,
                ValueNorm: unitValue.Value.ValueNorm,
                ParseStatus: "ok",
                ParseError: null,
                Source: raw);
        }

        var prepared = raw
            .Replace('Ø', '∅')
            .Replace('Ф', '∅')
            .Replace('ф', '∅');

        if (TryParseDiameterLength(prepared, out var diameter, out var length))
        {
            return Success(
                shapeType: "rod",
                diameterMm: diameter,
                thicknessMm: null,
                widthMm: null,
                lengthMm: length,
                unitNorm: normalizedUnit,
                valueNorm: normalizedValue,
                source: raw,
                parseStatus: "ok");
        }

        var numbers = ParseNumbers(DelimiterRegex.Split(prepared));
        if (numbers.Count == 3)
        {
            return Success(
                shapeType: "plate",
                diameterMm: null,
                thicknessMm: numbers[0],
                widthMm: numbers[1],
                lengthMm: numbers[2],
                unitNorm: normalizedUnit,
                valueNorm: normalizedValue,
                source: raw,
                parseStatus: "ok");
        }

        if (numbers.Count == 2)
        {
            return Success(
                shapeType: "sheet",
                diameterMm: null,
                thicknessMm: null,
                widthMm: numbers[0],
                lengthMm: numbers[1],
                unitNorm: normalizedUnit,
                valueNorm: normalizedValue,
                source: raw,
                parseStatus: "ok");
        }

        var onlyOneNumber = ParseSingleNumber(prepared);
        if (onlyOneNumber is not null)
        {
            return new MetalSizeParseResult(
                ShapeType: "unknown",
                DiameterMm: null,
                ThicknessMm: null,
                WidthMm: null,
                LengthMm: null,
                UnitNorm: normalizedUnit,
                ValueNorm: normalizedValue,
                ParseStatus: "partial",
                ParseError: "Обнаружено только одно числовое значение без геометрического шаблона.",
                Source: raw);
        }

        return Failed(raw, normalizedUnit, normalizedValue, "Не удалось распознать поддерживаемый шаблон размера.");
    }

    private static MetalSizeParseResult Success(
        string shapeType,
        decimal? diameterMm,
        decimal? thicknessMm,
        decimal? widthMm,
        decimal? lengthMm,
        string unitNorm,
        decimal? valueNorm,
        string source,
        string parseStatus)
    {
        return new MetalSizeParseResult(
            ShapeType: shapeType,
            DiameterMm: diameterMm,
            ThicknessMm: thicknessMm,
            WidthMm: widthMm,
            LengthMm: lengthMm,
            UnitNorm: unitNorm,
            ValueNorm: valueNorm,
            ParseStatus: parseStatus,
            ParseError: null,
            Source: source);
    }

    private static MetalSizeParseResult Failed(string source, string unitNorm, decimal? valueNorm, string error)
    {
        return new MetalSizeParseResult(
            ShapeType: "unknown",
            DiameterMm: null,
            ThicknessMm: null,
            WidthMm: null,
            LengthMm: null,
            UnitNorm: unitNorm,
            ValueNorm: valueNorm,
            ParseStatus: "failed",
            ParseError: error,
            Source: source);
    }

    private static bool TryParseDiameterLength(string prepared, out decimal diameter, out decimal length)
    {
        diameter = default;
        length = default;

        var value = prepared.Trim();
        if (!value.StartsWith('∅'))
        {
            return false;
        }

        var parts = ParseNumbers(DelimiterRegex.Split(value[1..]));
        if (parts.Count != 2)
        {
            return false;
        }

        diameter = parts[0];
        length = parts[1];
        return true;
    }

    private static (decimal ValueNorm, string UnitNorm)? TryParseUnitValue(string raw)
    {
        var match = UnitValueRegex.Match(raw.Trim());
        if (!match.Success)
        {
            return null;
        }

        if (!TryParseDecimal(match.Groups["value"].Value, out var numeric))
        {
            return null;
        }

        return (numeric, NormalizeUnit(match.Groups["unit"].Value));
    }

    private static decimal? ParseSingleNumber(string value)
    {
        return TryParseDecimal(value.Trim(), out var number) ? number : null;
    }

    private static List<decimal> ParseNumbers(IEnumerable<string> chunks)
    {
        var values = new List<decimal>();
        foreach (var chunk in chunks)
        {
            var cleaned = chunk.Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return [];
            }

            if (!TryParseDecimal(cleaned, out var parsed))
            {
                return [];
            }

            values.Add(parsed);
        }

        return values;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        var normalized = text.Replace(',', '.').Trim();
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeUnit(string? unitRaw)
    {
        var unit = (unitRaw ?? string.Empty).Trim().ToLowerInvariant();
        return unit switch
        {
            "кг" or "kg" => "kg",
            "м" or "m" => "m",
            "м2" or "m2" or "м²" => "m2",
            "шт" or "pcs" => "pcs",
            _ => "pcs",
        };
    }
}

public sealed record MetalSizeParseResult(
    string ShapeType,
    decimal? DiameterMm,
    decimal? ThicknessMm,
    decimal? WidthMm,
    decimal? LengthMm,
    string UnitNorm,
    decimal? ValueNorm,
    string ParseStatus,
    string? ParseError,
    string Source);
