using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Pressio.Models;

namespace Pressio.Converters;

// Mapeia PressureCategory -> cor da faixa (para o chip de classificação).
public sealed class CategoryColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PressureCategory category)
            return new SolidColorBrush(Color.Parse(BloodPressureClassification.Color(category)));
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
