using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Pressio.Converters;

public sealed class ColorMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
