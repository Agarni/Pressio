using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Pressio.Converters;

// Aumenta a fonte somente no mobile (público mais velho) mantendo o desktop no tamanho base.
// ConverterParameter é o tamanho base (ex.: "12"); o tamanho real = base * Scale.
public sealed class MobileFontSizeConverter : IValueConverter
{
    private const double Scale = 2.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var baseSize = double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? b : 14;
        var mobile = value is true;
        return mobile ? baseSize * Scale : baseSize;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
