using System;

namespace UchetNZP.Shared;

public static class NameWithCodeFormatter
{
    public static string getNameWithCode(string in_name, string? in_code)
    {
        string name = string.IsNullOrWhiteSpace(in_name) ? string.Empty : in_name.Trim();
        string code = string.IsNullOrWhiteSpace(in_code) ? string.Empty : in_code.Trim();
        string ret = name;

        if (string.IsNullOrWhiteSpace(ret) && !string.IsNullOrWhiteSpace(code))
        {
            ret = code;
        }
        else if (!string.IsNullOrWhiteSpace(code) && !isCodePartOfName(ret, code))
        {
            ret = $"{ret} ({code})";
        }

        return ret;
    }

    private static bool isCodePartOfName(string in_name, string in_code)
    {
        bool ret = false;

        if (!string.IsNullOrWhiteSpace(in_name) && !string.IsNullOrWhiteSpace(in_code))
        {
            ret = in_name.IndexOf(in_code, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        return ret;
    }
}
