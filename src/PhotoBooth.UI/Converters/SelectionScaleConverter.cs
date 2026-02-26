using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PhotoBooth.UI.Converters;

public class SelectionScaleConverter : IValueConverter, IMultiValueConverter
{
    public static readonly SelectionScaleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedId && parameter is string targetId)
        {
            if (selectedId == targetId)
            {
                // Selected: Scale down to 0.95
                return new ScaleTransform(0.95, 0.95);
            }
        }
        // Normal: Scale 1.0
        return new ScaleTransform(1.0, 1.0);
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is string selectedId && values[1] is string targetId)
        {
            if (selectedId == targetId)
            {
                return new ScaleTransform(0.95, 0.95);
            }
        }
        return new ScaleTransform(1.0, 1.0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
