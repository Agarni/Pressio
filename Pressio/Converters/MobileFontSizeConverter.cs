using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Pressio.Converters;

// Aumenta a fonte somente no mobile (público mais velho) mantendo o desktop no tamanho base.
// O ConverterParameter é "base[;escala]" — ex.: "12;1.8" (tamanho 12, escala 1.8 no mobile).
// Sem escala, usa 1.0 (tamanho base). Textos usam ~1.8; controles usam ~1.2.
public sealed class MobileFontSizeConverter : IValueConverter
{
    private const double DefaultScale = 1.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var spec = parameter?.ToString() ?? "14";
        var parts = spec.Split(';');
        var baseSize = parts.Length > 0 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? b : 14;
        var scale = parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : DefaultScale;
        return value is true ? baseSize * scale : baseSize;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
