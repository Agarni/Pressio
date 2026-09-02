using System;
using System.Globalization;
using Pressio.Models;

namespace Pressio.Services;

public sealed record ParsedBloodPressure(int Systolic, int Diastolic)
{
    public string DisplayValue => $"{Systolic / 10d:0.#}/{Diastolic / 10d:0.#}";
}

public static class BloodPressureParser
{
    public static bool TryParse(string? input, out ParsedBloodPressure? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Informe a pressão no formato 13/8 ou 130/80.";
            return false;
        }

        var parts = input.Trim().Replace('x', '/').Replace('X', '/').Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var first) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var second))
        {
            error = "Use o formato 13/8 ou 130/80.";
            return false;
        }

        var systolic = first < 30 ? first * 10 : first;
        var diastolic = second < 30 ? second * 10 : second;

        if (systolic is < 50 or > 300 || diastolic is < 30 or > 200)
        {
            error = "Confira os valores informados e tente novamente.";
            return false;
        }

        result = new ParsedBloodPressure(systolic, diastolic);
        return true;
    }
}
