using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace PhotoBooth.Event.Views;

public partial class CaptureView : UserControl
{
    public CaptureView()
    {
        InitializeComponent();
    }

    private void OnStartBtnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Image img)
        {
            img.RenderTransformOrigin = RelativePoint.Center;
            img.RenderTransform = TransformOperations.Parse("scale(0.92, 0.92)");
        }
        e.Handled = true;
    }

    private void OnStartBtnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Image img)
        {
            img.RenderTransformOrigin = RelativePoint.Center;
            img.RenderTransform = TransformOperations.Parse("scale(1, 1)");
        }

        // Fire the command only when the user releases
        if (DataContext is ViewModels.CaptureViewModel vm && vm.StartShootingSequenceCommand.CanExecute(null))
        {
            vm.StartShootingSequenceCommand.Execute(null);
        }
        e.Handled = true;
    }
}
