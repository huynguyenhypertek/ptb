using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace PhotoBooth.Event.Views;

public partial class ReviewPrintView : UserControl
{
    public ReviewPrintView()
    {
        InitializeComponent();
    }

    private void OnNextBtnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Image img)
        {
            img.RenderTransformOrigin = RelativePoint.Center;
            img.RenderTransform = TransformOperations.Parse("scale(0.92, 0.92)");
        }
        e.Handled = true;
    }

    private void OnNextBtnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Image img)
        {
            img.RenderTransformOrigin = RelativePoint.Center;
            img.RenderTransform = TransformOperations.Parse("scale(1, 1)");
        }

        if (DataContext is ViewModels.ReviewPrintViewModel vm && vm.ReturnToStartCommand.CanExecute(null))
        {
            vm.ReturnToStartCommand.Execute(null);
        }
        e.Handled = true;
    }
}
