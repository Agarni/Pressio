using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Pressio.Converters;

// true se o valor (binding) é igual ao parâmetro (ex.: seleção de período do gráfico).
public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
