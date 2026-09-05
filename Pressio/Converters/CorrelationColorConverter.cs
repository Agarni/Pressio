using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Pressio.Converters;

// Cor da correlação: vermelho se o fator aumenta a pressão, verde se reduz.
public sealed class CorrelationColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.Parse("#D64545"))
            : new SolidColorBrush(Color.Parse("#2E9E6B"));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
