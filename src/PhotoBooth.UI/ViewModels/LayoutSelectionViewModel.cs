using System;
using System.Threading;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Models;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 2: Layout Selection
/// </summary>
public partial class LayoutSelectionViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private ObservableCollection<Layout> _layouts = new();

    [ObservableProperty]
    private Layout? _selectedLayout;

    // Track selected layout ID for visual binding
    [ObservableProperty]
    private string? _selectedLayoutId;

    [ObservableProperty]
    private string _warningMessage = "";

    private readonly CancellationTokenSource _cts = new();

    public LayoutSelectionViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadLayouts();
    }

    private void LoadLayouts()
    {
        Console.WriteLine($"[LAYOUT] Loading layouts for plan: {DeviceConfig.PlanType}");
        
        // Pro: show all layouts
        // Basic: only layout2
        if (DeviceConfig.PlanType == "Pro")
        {
            Layouts = new ObservableCollection<Layout>
            {
                new Layout 
                { 
                    Id = "layout6", 
                    Name = "Layout 6 ảnh", 
                    CaptureCount = 8,
                    SelectCount = 6
                },
                new Layout 
                { 
                    Id = "layout2", 
                    Name = "Layout 2 ảnh", 
                    CaptureCount = 4,
                    SelectCount = 2
                }
            };
        }
        else // Basic
        {
            Layouts = new ObservableCollection<Layout>
            {
                new Layout 
                { 
                    Id = "layout2", 
                    Name = "Layout 2 ảnh", 
                    CaptureCount = 4,
                    SelectCount = 2
                }
            };
        }
    }

    /// <summary>
    /// Select layout 6 photos
    /// </summary>
    [RelayCommand]
    private void SelectLayout6()
    {
        var layout = Layouts.FirstOrDefault(l => l.Id == "layout6");
        if (layout == null)
        {
            WarningMessage = "⚠️ Gói Basic không hỗ trợ layout này! Vui lòng nâng cấp lên Pro.";
            _ = ClearWarningAfterDelay();
            return;
        }
        WarningMessage = "";
        SelectedLayout = layout;
        SelectedLayoutId = "layout6";
        SessionService.SetLayout(SelectedLayout);
        Console.WriteLine($"Selected Layout 6: {SelectedLayout.Name}");
    }

    /// <summary>
    /// Select layout 2 photos
    /// </summary>
    [RelayCommand]
    private void SelectLayout2()
    {
        var layout = Layouts.FirstOrDefault(l => l.Id == "layout2");
        if (layout == null) return;
        SelectedLayout = layout;
        SelectedLayoutId = "layout2";
        SessionService.SetLayout(SelectedLayout);
        Console.WriteLine($"Selected Layout 2: {SelectedLayout.Name}");
    }

    /// <summary>
    /// Navigate to next screen (Frame Selection)
    /// </summary>
    [RelayCommand]
    private void GoNext()
    {
        Console.WriteLine($"GoNext called, SelectedLayout: {SelectedLayout?.Name ?? "null"}");
        if (SelectedLayout != null)
        {
            Console.WriteLine("Navigating to BackgroundSelectionViewModel...");
            NavigationService.NavigateTo<BackgroundSelectionViewModel>();
        }
        else
        {
            Console.WriteLine("No layout selected - cannot navigate");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<StartViewModel>();
    }

    private async Task ClearWarningAfterDelay()
    {
        try
        {
            await Task.Delay(3000, _cts.Token);
            WarningMessage = "";
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed before delay completes
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
