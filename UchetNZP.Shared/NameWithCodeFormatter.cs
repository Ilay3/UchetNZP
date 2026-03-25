using System;
using System.Text.RegularExpressions;

namespace UchetNZP.Shared;

public static class NameWithCodeFormatter
{
    public static string getNameWithCode(string? in_name, string? in_code)
    {
        string name = NormalizeValue(in_name);
        string code = NormalizeValue(in_code);
        string ret = name;

        if (string.IsNullOrWhiteSpace(ret) && !string.IsNullOrWhiteSpace(code))
        {
            ret = code;
        }
        else if (HasDistinctCode(ret, code))
        {
            ret = $"{ret} / {code}";
        }

        return ret;
    }

    public static bool HasDistinctCode(string? in_name, string? in_code)
    {
        string name = NormalizeValue(in_name);
        string code = NormalizeValue(in_code);

        return !string.IsNullOrWhiteSpace(code) && !ContainsNormalizedValue(name, code);
    }

    private static bool ContainsNormalizedValue(string in_name, string in_code)
    {
        if (string.IsNullOrWhiteSpace(in_name) || string.IsNullOrWhiteSpace(in_code))
        {
            return false;
        }

        string normalizedName = NormalizeForComparison(in_name);
        string normalizedCode = NormalizeForComparison(in_code);

        return normalizedName.Contains(normalizedCode, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string NormalizeForComparison(string value)
    {
        string normalized = NormalizeValue(value);

        return Regex.Replace(normalized, @"[\s()/.\\_-]+", string.Empty);
    }
}
