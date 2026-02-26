using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Infrastructure.Services;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 7: Photo Capture with camera preview and countdown
/// </summary>
public partial class CaptureViewModel : ViewModelBase, IDisposable
{
    private readonly ICameraService _cameraService;
    private readonly string _photosDirectory;
    
    [ObservableProperty]
    private int _currentPhotoIndex = 0;

    [ObservableProperty]
    private int _totalPhotos = 8;

    [ObservableProperty]
    private int _countdown = 3;

    [ObservableProperty]
    private bool _isCountingDown;

    [ObservableProperty]
    private bool _isCameraReady;

    [ObservableProperty]
    private bool _isShootingInProgress;

    [ObservableProperty]
    private bool _isLayout6;

    [ObservableProperty]
    private bool _isLayout2 = true;

    [ObservableProperty]
    private bool _isFlashing;

    [ObservableProperty]
    private Bitmap? _cameraPreview;

    [ObservableProperty]
    private ObservableCollection<string> _capturedPhotos = new();

    [ObservableProperty]
    private string _statusMessage = "Đang kết nối camera...";

    [ObservableProperty]
    private Bitmap? _backgroundImage;

    public CaptureViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        TotalPhotos = SessionService.CurrentSession.SelectedLayout?.CaptureCount ?? 8;
        
        LoadCaptureBackground();
        
        // Create photos directory
        _photosDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "PhotoBooth",
            DateTime.Now.ToString("yyyyMMdd_HHmmss")
        );
        
        // Initialize camera
        _cameraService = new CameraService();
        InitializeCameraAsync();
    }

    private void LoadCaptureBackground()
    {
        var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
        string imagePath;
        
        if (layoutId == "layout6")
        {
            imagePath = "avares://PhotoBooth.UI/Assets/backgrounds/back7_anh6.png";
            IsLayout6 = true;
            IsLayout2 = false;
        }
        else // layout2
        {
            imagePath = "avares://PhotoBooth.UI/Assets/backgrounds/back7_anh2.png";
            IsLayout6 = false;
            IsLayout2 = true;
        }

        try
        {
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(imagePath)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load capture background: {ex.Message}");
        }
    }

    private async void InitializeCameraAsync()
    {
        await Task.Run(() =>
        {
            if (_cameraService.Initialize(0))
            {
                _cameraService.FrameReady += OnFrameReady;
                _cameraService.StartPreview();
                
                Dispatcher.UIThread.Post(() =>
                {
                    IsCameraReady = true;
                    StatusMessage = "Sẵn sàng chụp!";
                });
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = "Không tìm thấy camera!";
                });
            }
        });
    }

    private void OnFrameReady(object? sender, byte[] frameBytes)
    {
        try
        {
            using var stream = new MemoryStream(frameBytes);
            var bitmap = new Bitmap(stream);
            
            Dispatcher.UIThread.Post(() =>
            {
                var oldBitmap = CameraPreview;
                CameraPreview = bitmap;
                oldBitmap?.Dispose();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Frame display error: {ex.Message}");
        }
    }

    [ObservableProperty]
    private int _countdownDuration = 1; // 5 seconds countdown

    [RelayCommand]
    private async Task StartShootingSequenceAsync()
    {
        if (!IsCameraReady || IsShootingInProgress) return;
        
        IsShootingInProgress = true;
        
        // Reset if starting over (though usually we start from 0)
        if (CurrentPhotoIndex >= TotalPhotos) CurrentPhotoIndex = 0;

        StatusMessage = "Bắt đầu chụp...";

        while (CurrentPhotoIndex < TotalPhotos)
        {
            // 1. Countdown
            IsCountingDown = true;
            for (int i = CountdownDuration; i > 0; i--)
            {
                Countdown = i;
                StatusMessage = $"Chụp tấm {CurrentPhotoIndex + 1}/{TotalPhotos} trong {i}s...";
                await Task.Delay(1000);
            }
            IsCountingDown = false;

            // 2. Capture
            await CapturePhotoAsync();

            // 3. Short delay after capture to show preview or just breather
            if (CurrentPhotoIndex < TotalPhotos)
            {
                await Task.Delay(1000); // 1s delay (review) before next countdown
            }
        }
    }

    private async Task CapturePhotoAsync()
    {
        try
        {
            StatusMessage = "📸 Chụp!";
            
            // Flash effect
            IsFlashing = true;
            
            // Capture photo
            var photoPath = _cameraService.CapturePhoto(_photosDirectory);
            
            // Keep flash visible briefly
            await Task.Delay(200);
            IsFlashing = false;
            
            // Crop ảnh theo layout
            var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
            if (layoutId == "layout2")
            {
                // Layout 2 ảnh: crop về 518x720, dịch phải 50px
                ImageCropService.CropToSize(photoPath, 518, 720, offsetX: 50);
            }
            else if (layoutId == "layout6")
            {
                // Layout 6 ảnh: crop về 659x720, dịch phải 50px
                ImageCropService.CropToSize(photoPath, 659, 720, offsetX: 50);
            }
            
            CapturedPhotos.Add(photoPath);
            SessionService.AddCapturedPhoto(photoPath);
            
            CurrentPhotoIndex++;
            
            StatusMessage = $"Đã chụp {CurrentPhotoIndex}/{TotalPhotos}";

            if (CurrentPhotoIndex >= TotalPhotos)
            {
                StatusMessage = "Hoàn thành! Đang chuyển...";
                await Task.Delay(1000);
                
                // Stop camera before navigating
                _cameraService.StopPreview();
                NavigationService.NavigateTo<PhotoSelectionViewModel>();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi chụp ảnh: {ex.Message}";
            IsCountingDown = false;
        }
    }

    [RelayCommand]
    private void Skip()
    {
        _cameraService.StopPreview();
        NavigationService.NavigateTo<PhotoSelectionViewModel>();
    }

    public void Dispose()
    {
        _cameraService.FrameReady -= OnFrameReady;
        _cameraService.Dispose();
        CameraPreview?.Dispose();
    }
}
