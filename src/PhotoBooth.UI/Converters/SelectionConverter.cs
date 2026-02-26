using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PhotoBooth.UI.Converters;

public class SelectionConverter : IValueConverter
{
    public static readonly SelectionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedId && parameter is string targetId)
        {
            if (selectedId == targetId)
            {
                // Return Yellow Brush
                return new SolidColorBrush(Color.Parse("#FFD93D"));
            }
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
