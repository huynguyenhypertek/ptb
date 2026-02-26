using Avalonia.Controls;
using Avalonia.Input;
using PhotoBooth.Admin.ViewModels;

namespace PhotoBooth.Admin.Views;

public partial class FrameListView : UserControl
{
    public FrameListView()
    {
        InitializeComponent();
    }

    private void OnFrameClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is FrameDisplayItem frame
            && DataContext is FrameListViewModel vm)
        {
            vm.SelectFrameCommand.Execute(frame);
        }
    }
}
