using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Models;
using PhotoBooth.UI.Services;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Media.Imaging;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 3: Background Selection - loads frames from API based on store permissions
/// </summary>
public partial class BackgroundSelectionViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private Thickness _containerMargin;

    [ObservableProperty]
    private double _containerSpacing;

    [ObservableProperty]
    private Bitmap? _option1Image;
    [ObservableProperty]
    private string _option1Name = "";
    
    [ObservableProperty]
    private Bitmap? _option2Image;
    [ObservableProperty]
    private string _option2Name = "";
    
    // Keep track of the actual background objects corresponding to options
    private Background? _bg1;
    private Background? _bg2;

    [ObservableProperty]
    private Background? _selectedBackground;

    // Track selected background ID for visual binding
    [ObservableProperty]
    private string? _selectedBackgroundId;


    private bool _disposed;
    private readonly CancellationTokenSource _cts = new();

    public BackgroundSelectionViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        _ = LoadBackgroundsAsync();
    }

    private async Task LoadBackgroundsAsync()
    {
        var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
        Console.WriteLine($"[DEBUG] Loading Backgrounds for Layout: {layoutId}");

        // Set spacing based on layout
        if (layoutId == "layout6")
        {
            ContainerMargin = new Thickness(0, 40, 0, 0);
            ContainerSpacing = 300;
        }
        else
        {
            ContainerMargin = new Thickness(-80, 50, 50, 0);
            ContainerSpacing = 300;
        }

        // Try loading from API (store-permitted frames)
        try
        {
            var storeId = DeviceConfig.StoreId;
            if (storeId > 0)
            {
                var client = HttpService.Client;
                var apiBase = DeviceConfig.ApiBaseUrl;
                
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                var frames = await client.GetFromJsonAsync<ApiFrame[]>(
                    $"{apiBase}/api/frames/store/{storeId}?layoutType={layoutId}", timeoutCts.Token);

                if (frames != null && frames.Length > 0)
                {
                    Console.WriteLine($"[BG] Loaded {frames.Length} frames from API for store {storeId}");
                    
                    // Load first frame
                    if (frames.Length >= 1)
                    {
                        _bg1 = new Background
                        {
                            Id = $"api_frame_{frames[0].Id}",
                            Name = frames[0].Name,
                            ImagePath = $"{apiBase}/api/frames/{frames[0].Id}/image"
                        };
                        Option1Name = _bg1.Name;
                        var img1 = await LoadBitmapFromUrl(_bg1.ImagePath);
                        if (_disposed) { img1?.Dispose(); return; }
                        var oldOpt1 = Option1Image;
                        Option1Image = img1;
                        oldOpt1?.Dispose();
                    }

                    // Load second frame
                    if (frames.Length >= 2)
                    {
                        _bg2 = new Background
                        {
                            Id = $"api_frame_{frames[1].Id}",
                            Name = frames[1].Name,
                            ImagePath = $"{apiBase}/api/frames/{frames[1].Id}/image"
                        };
                        Option2Name = _bg2.Name;
                        var img2 = await LoadBitmapFromUrl(_bg2.ImagePath);
                        if (_disposed) { img2?.Dispose(); return; }
                        var oldOpt2 = Option2Image;
                        Option2Image = img2;
                        oldOpt2?.Dispose();
                    }

                    return; // Success - don't fall through to defaults
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BG] API load failed, falling back to defaults: {ex.Message}");
        }

        // Fallback to hardcoded defaults
        LoadDefaultBackgrounds(layoutId);
    }

    private void LoadDefaultBackgrounds(string? layoutId)
    {
        if (_disposed) return;
        Console.WriteLine("[BG] Using default built-in frames");
        
        if (layoutId == "layout6")
        {
            _bg1 = new Background { Id = "frame6_1", Name = "Frame 6 - Option 1", ImagePath = "avares://PhotoBooth.UI/Assets/frames/nen6_1.png" };
            _bg2 = new Background { Id = "frame6_2", Name = "Frame 6 - Option 2", ImagePath = "avares://PhotoBooth.UI/Assets/frames/nen6_2.png" };
        }
        else
        {
            _bg1 = new Background { Id = "frame2_1", Name = "Frame 2 - Option 1", ImagePath = "avares://PhotoBooth.UI/Assets/frames/nen2_1.png" };
            _bg2 = new Background { Id = "frame2_2", Name = "Frame 2 - Option 2", ImagePath = "avares://PhotoBooth.UI/Assets/frames/nen2_2.png" };
        }
        
        Option1Name = _bg1.Name;
        Option2Name = _bg2.Name;
        var oldOpt1 = Option1Image;
        Option1Image = LoadBitmapFromAsset(_bg1.ImagePath);
        oldOpt1?.Dispose();
        var oldOpt2 = Option2Image;
        Option2Image = LoadBitmapFromAsset(_bg2.ImagePath);
        oldOpt2?.Dispose();
    }

    private async Task<Bitmap?> LoadBitmapFromUrl(string url)
    {
        try
        {
            var client = HttpService.Client;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            var bytes = await client.GetByteArrayAsync(url, timeoutCts.Token);
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load bitmap from URL: {url}. Error: {ex.Message}");
            return null;
        }
    }
    
    private Bitmap? LoadBitmapFromAsset(string uri)
    {
        try
        {
            return new Bitmap(AssetLoader.Open(new Uri(uri)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load bitmap: {uri}. Error: {ex.Message}");
            return null;
        }
    }

    [RelayCommand]
    private void SelectOption1()
    {
        if (_bg1 == null) return;
        SelectedBackground = _bg1;
        SelectedBackgroundId = _bg1.Id;
        SessionService.SetBackground(_bg1);
        Console.WriteLine($"Selected Option 1: {_bg1.Name}");
    }

    [RelayCommand]
    private void SelectOption2()
    {
        if (_bg2 == null) return;
        SelectedBackground = _bg2;
        SelectedBackgroundId = _bg2.Id;
        SessionService.SetBackground(_bg2);
        Console.WriteLine($"Selected Option 2: {_bg2.Name}");
    }

    /// <summary>
    /// Navigate to next screen (Frame Selection)
    /// </summary>
    [RelayCommand]
    private void GoNext()
    {
        Console.WriteLine($"GoNext called, SelectedBackground: {SelectedBackground?.Name ?? "null"}");
        if (SelectedBackground != null)
        {
            Console.WriteLine("Navigating to PaymentAmountViewModel...");
            NavigationService.NavigateTo<PaymentAmountViewModel>();
        }
        else
        {
            Console.WriteLine("No background selected - cannot navigate");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<LayoutSelectionViewModel>();
    }

    public void Dispose()
    {
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        Option1Image?.Dispose();
        Option1Image = null;
        Option2Image?.Dispose();
        Option2Image = null;
    }
}

// Simple model for API response
public class ApiFrame
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LayoutType { get; set; } = "";
    public string FileName { get; set; } = "";
}
