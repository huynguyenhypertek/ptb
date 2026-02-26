using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace PhotoBooth.UI.Views;

public partial class PrintingView : UserControl
{
    public PrintingView()
    {
        InitializeComponent();
    }
}

public class ProgressToWidthConverter : IValueConverter
{
    public double MaxWidth { get; set; } = 600;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int progress)
        {
            return (progress / 100.0) * MaxWidth;
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
