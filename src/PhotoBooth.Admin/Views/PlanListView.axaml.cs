using Avalonia.Controls;
using Avalonia.Input;
using PhotoBooth.Admin.ViewModels;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.Views;

public partial class PlanListView : UserControl
{
    public PlanListView()
    {
        InitializeComponent();
    }

    private void OnPlanClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is PlanItem plan
            && DataContext is PlanListViewModel vm)
        {
            vm.SelectedPlan = plan;
        }
    }
}
