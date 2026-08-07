using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace PhotoBooth.Event.Views;

public partial class StartView : UserControl
{
    public StartView()
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
        // Prevent the full-screen button underneath from also firing
        e.Handled = true;
    }

    private void OnNextBtnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Image img)
        {
            img.RenderTransformOrigin = RelativePoint.Center;
            img.RenderTransform = TransformOperations.Parse("scale(1, 1)");
        }

        // Fire the command only when the user releases
        if (DataContext is ViewModels.StartViewModel vm && vm.StartCommand.CanExecute(null))
        {
            vm.StartCommand.Execute(null);
        }
        e.Handled = true;
    }
}
