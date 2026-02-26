using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PhotoBooth.Admin.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? new SolidColorBrush(Color.Parse("#22c55e")) : new SolidColorBrush(Color.Parse("#e94560"));
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "🟢 Hoạt động" : "🔴 Ngưng";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToToggleTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "⏸ Ngưng" : "▶ Kích hoạt";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
