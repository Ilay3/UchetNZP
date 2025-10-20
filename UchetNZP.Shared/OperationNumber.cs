using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace UchetNZP.Shared;

public static class OperationNumber
{
    public const string AllowedPattern = @"^\d{1,10}(?:/\d{1,5})?$";

    public static bool TryParse(string? value, out int result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = 0;
            return false;
        }

        var trimmed = value.Trim();
        if (!Regex.IsMatch(trimmed, AllowedPattern, RegexOptions.CultureInvariant))
        {
            result = 0;
            return false;
        }

        var slashIndex = trimmed.IndexOf('/', StringComparison.Ordinal);
        var numericPart = slashIndex >= 0 ? trimmed[..slashIndex] : trimmed;

        return int.TryParse(numericPart, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    public static int Parse(string? value, string parameterName)
    {
        if (!TryParse(value, out var result))
        {
            throw new ArgumentException("Номер операции должен содержать от 1 до 10 цифр и может включать дробную часть через «/».", parameterName);
        }

        return result;
    }

    public static string Format(int opNumber)
    {
        return opNumber.ToString("D3", CultureInfo.InvariantCulture);
    }

    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
