using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PhotoBooth.Infrastructure.Services;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI.ViewModels;

/// <summary>
/// Screen 8: Select best photos to use in final layout
/// </summary>
public partial class PhotoSelectionViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private ObservableCollection<PhotoItem> _photos = new();

    [ObservableProperty]
    private int _requiredSelections = 2;

    [ObservableProperty]
    private Bitmap? _backgroundImage;

    [ObservableProperty]
    private Bitmap? _framePreviewImage;

    // Layout-specific photo grid settings
    [ObservableProperty]
    private double _photoWidth = 253;

    [ObservableProperty]
    private double _photoHeight = 351;

    [ObservableProperty]
    private Avalonia.Thickness _photoMargin = new(21.5);

    [ObservableProperty]
    private double _gridMaxWidth = 1500;

    [ObservableProperty]
    private Avalonia.Thickness _gridMargin = new(131, 90, 0, 80);

    // Selected photo previews for the frame
    [ObservableProperty]
    private Bitmap? _selectedPhoto1;

    [ObservableProperty]
    private Bitmap? _selectedPhoto2;

    [ObservableProperty]
    private Bitmap? _selectedPhoto3;

    [ObservableProperty]
    private Bitmap? _selectedPhoto4;

    [ObservableProperty]
    private Bitmap? _selectedPhoto5;

    [ObservableProperty]
    private Bitmap? _selectedPhoto6;

    [ObservableProperty]
    private bool _isLayout6;

    [ObservableProperty]
    private bool _isLayout2 = true;

    public int SelectedCount => Photos.Count(p => p.IsSelected);

    private bool _disposed;
    private readonly CancellationTokenSource _cts = new();

    public PhotoSelectionViewModel(NavigationService navigationService, SessionService sessionService) 
        : base(navigationService, sessionService)
    {
        LoadBackground();
        LoadFramePreview();
        LoadCapturedPhotos();
    }

    private void LoadBackground()
    {
        var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
        string bgPath;
        
        if (layoutId == "layout6")
        {
            bgPath = "avares://PhotoBooth.UI/Assets/backgrounds/nen8 lua chon anh 6.png";
            // 8 photos: 2 rows x 4 columns
            PhotoWidth = 253;
            PhotoHeight = 285;
            PhotoMargin = new Avalonia.Thickness(11.42,11.95);
            GridMaxWidth = 1200;
            GridMargin = new Avalonia.Thickness(79, 0, 0, 80);
            IsLayout6 = true;
            IsLayout2 = false;
        }
        else
        {
            bgPath = "avares://PhotoBooth.UI/Assets/backgrounds/back8_2.png";
            // 4 photos: 1 row x 4 columns
            PhotoWidth = 253;
            PhotoHeight = 351;
            PhotoMargin = new Avalonia.Thickness(21.5);
            GridMaxWidth = 1500;
            GridMargin = new Avalonia.Thickness(131, 90, 0, 80);
            IsLayout6 = false;
            IsLayout2 = true;
        }
        
        try
        {
            var oldBg = BackgroundImage;
            BackgroundImage = new Bitmap(AssetLoader.Open(new Uri(bgPath)));
            oldBg?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load background: {bgPath}. {ex.Message}");
        }
    }

    private void LoadFramePreview()
    {
        var bgId = SessionService.CurrentSession.SelectedBackground?.Id;
        var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
        
        // Set required selections based on layout
        if (layoutId == "layout6")
            RequiredSelections = 6;
        else
            RequiredSelections = 2;

        // API frame: download from server
        if (bgId != null && bgId.StartsWith("api_frame_"))
        {
            _ = LoadFrameFromApiAsync(bgId);
            return;
        }
        
        // Built-in frame
        string framePath;
        if (bgId == "frame6_1")
            framePath = "avares://PhotoBooth.UI/Assets/finish/nen6_1.png";
        else if (bgId == "frame6_2")
            framePath = "avares://PhotoBooth.UI/Assets/finish/nen6_2.png";
        else if (bgId == "frame2_1")
            framePath = "avares://PhotoBooth.UI/Assets/finish/nen2_1.png";
        else if (bgId == "frame2_2")
            framePath = "avares://PhotoBooth.UI/Assets/finish/nen2_2.png";
        else
            framePath = "avares://PhotoBooth.UI/Assets/finish/nen2_1.png";
        
        try
        {
            var oldFrame = FramePreviewImage;
            FramePreviewImage = new Bitmap(AssetLoader.Open(new Uri(framePath)));
            oldFrame?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load frame preview: {framePath}. {ex.Message}");
        }
    }

    private async Task LoadFrameFromApiAsync(string bgId)
    {
        try
        {
            var frameIdStr = bgId.Replace("api_frame_", "");
            var url = $"{DeviceConfig.ApiBaseUrl}/api/frames/{frameIdStr}/image";
            Console.WriteLine($"[PREVIEW] Loading frame from API: {url}");
            
            var client = HttpService.Client;
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            // Finding 1: Dispose HttpResponseMessage to prevent connection leak
            using var response = await client.GetAsync(url, timeoutCts.Token);
            response.EnsureSuccessStatusCode();
            // Finding 3/6: Pass cancellation token to body read
            var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
            using var ms = new MemoryStream(bytes);
            var newFrame = new Bitmap(ms);
            if (_disposed) { newFrame.Dispose(); return; }
            var oldFrame = FramePreviewImage;
            FramePreviewImage = newFrame;
            oldFrame?.Dispose();
            
            Console.WriteLine($"[PREVIEW] Frame loaded OK from API (id={frameIdStr})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load API frame preview: {ex.Message}");
            // Fallback to default
            try
            {
                var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
                var fallback = layoutId == "layout6" 
                    ? "avares://PhotoBooth.UI/Assets/finish/nen6_1.png"
                    : "avares://PhotoBooth.UI/Assets/finish/nen2_1.png";
                var newFallback = new Bitmap(AssetLoader.Open(new Uri(fallback)));
                if (_disposed) { newFallback.Dispose(); return; }
                var oldFallback = FramePreviewImage;
                FramePreviewImage = newFallback;
                oldFallback?.Dispose();
            }
            catch { }
        }
    }

    private void LoadCapturedPhotos()
    {
        var paths = SessionService.CurrentSession.CapturedPhotoPaths;
        var layoutId = SessionService.CurrentSession.SelectedLayout?.Id;
        RequiredSelections = SessionService.CurrentSession.SelectedLayout?.SelectCount ?? 2;
        
        // Dispose old thumbnails before replacing
        foreach (var oldPhoto in Photos)
        {
            oldPhoto.Thumbnail?.Dispose();
            oldPhoto.Thumbnail = null;
        }
        
        Photos = new ObservableCollection<PhotoItem>(
            paths.Select((p, i) => new PhotoItem 
            { 
                Index = i, 
                Path = p,
                Thumbnail = LoadThumbnail(p)
            })
        );
    }

    private Bitmap? LoadThumbnail(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var fileStream = File.OpenRead(path);
                using var memStream = new MemoryStream();
                fileStream.CopyTo(memStream);
                memStream.Position = 0;
                return new Bitmap(memStream);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load thumbnail: {path}. {ex.Message}");
        }
        return null;
    }

    // Track selection order
    private readonly List<PhotoItem> _selectionOrder = new();

    [RelayCommand]
    private void ToggleSelection(PhotoItem photo)
    {
        if (photo.IsSelected)
        {
            photo.IsSelected = false;
            _selectionOrder.Remove(photo);
        }
        else if (SelectedCount < RequiredSelections)
        {
            photo.IsSelected = true;
            _selectionOrder.Add(photo);
        }
        OnPropertyChanged(nameof(SelectedCount));
        UpdatePreviewPhotos();
    }

    private void UpdatePreviewPhotos()
    {
        // Use selection order, not capture order
        SelectedPhoto1 = _selectionOrder.Count > 0 ? _selectionOrder[0].Thumbnail : null;
        SelectedPhoto2 = _selectionOrder.Count > 1 ? _selectionOrder[1].Thumbnail : null;
        SelectedPhoto3 = _selectionOrder.Count > 2 ? _selectionOrder[2].Thumbnail : null;
        SelectedPhoto4 = _selectionOrder.Count > 3 ? _selectionOrder[3].Thumbnail : null;
        SelectedPhoto5 = _selectionOrder.Count > 4 ? _selectionOrder[4].Thumbnail : null;
        SelectedPhoto6 = _selectionOrder.Count > 5 ? _selectionOrder[5].Thumbnail : null;
    }

    [RelayCommand]
    private async Task Confirm()
    {
        // Save selected indices based on selection order
        var selectedIndices = _selectionOrder.Select(p => p.Index).ToList();
        SessionService.SetSelectedPhotos(selectedIndices);
        
        // Compose photo + frame immediately and save to session folder
        string? tempFramePath = null;
        try
        {
            var session = SessionService.CurrentSession;
            var allPhotos = session.CapturedPhotoPaths;
            
            // Get selected photo paths in selection order
            var selectedPhotoPaths = selectedIndices
                .Where(i => i >= 0 && i < allPhotos.Count)
                .Select(i => allPhotos[i])
                .ToArray();
            
            // Determine frame from finish/ folder or API
            var bgId = session.SelectedBackground?.Id;
            var layoutId = session.SelectedLayout?.Id;
            tempFramePath = Path.Combine(Path.GetTempPath(), $"photobooth_frame_{Guid.NewGuid():N}.png");
            
            if (bgId != null && bgId.StartsWith("api_frame_"))
            {
                // Download frame from API
                var frameIdStr = bgId.Replace("api_frame_", "");
                var url = $"{DeviceConfig.ApiBaseUrl}/api/frames/{frameIdStr}/image";
                Console.WriteLine($"[COMPOSITE] Downloading frame from API: {url}");
                var httpClient = HttpService.Client;
                using var dlCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                dlCts.CancelAfter(TimeSpan.FromSeconds(15));
                var bytes = await httpClient.GetByteArrayAsync(url, dlCts.Token);
                await File.WriteAllBytesAsync(tempFramePath, bytes);
            }
            else
            {
                // Use built-in asset
                string frameName;
                if (bgId == "frame6_1") frameName = "nen6_1.png";
                else if (bgId == "frame6_2") frameName = "nen6_2.png";
                else if (bgId == "frame2_2") frameName = "nen2_2.png";
                else frameName = "nen2_1.png";
                
                string frameAssetPath = $"avares://PhotoBooth.UI/Assets/finish/{frameName}";
                using (var stream = AssetLoader.Open(new Uri(frameAssetPath)))
                using (var fileStream = File.Create(tempFramePath))
                {
                    stream.CopyTo(fileStream);
                }
            }
            
            // Photo positions based on layout
            (int x, int y, int w, int h)[] positions;
            
            if (layoutId == "layout6")
            {
                // Layout 6: 6 photos in 3x2 grid (664x990 frame) - 3% zoom to fill gaps
                positions = new (int, int, int, int)[]
                {
                    (73, 50, 247, 269),    // Photo 1: top-left
                    (343, 50, 247, 269),   // Photo 2: top-right
                    (74, 326, 247, 269),   // Photo 3: middle-left
                    (344, 326, 247, 269),  // Photo 4: middle-right
                    (73, 602, 247, 269),   // Photo 5: bottom-left
                    (343, 602, 247, 269),  // Photo 6: bottom-right
                };
            }
            else
            {
                // Layout 2: 2 photos stacked (682x2048 frame)
                positions = new (int, int, int, int)[]
                {
                    (52, 113, 578, 801),   // Photo 1: top hole
                    (52, 962, 578, 801),   // Photo 2: bottom hole
                };
            }
            
            // Save composite to session folder
            string sessionDir = Path.GetDirectoryName(allPhotos[0]) ?? Path.GetTempPath();
            string outputPath = Path.Combine(sessionDir, "final_composite.png");
            
            ImageCompositeService.Compose(tempFramePath, selectedPhotoPaths, positions, outputPath);
            
            // D1: Overlay sequential number with try-catch — photo without number is still usable
            try
            {
                var seqNumber = SessionService.CurrentSession.SequentialNumber;
                if (!string.IsNullOrEmpty(seqNumber))
                {
                    var backupPath = ImageCompositeService.OverlaySequentialNumber(outputPath, seqNumber);
                    if (backupPath != null)
                        Console.WriteLine($"[COMPOSITE] Original backed up to: {Path.GetFileName(backupPath)}");
                }
            }
            catch (Exception overlayEx)
            {
                Console.WriteLine($"[COMPOSITE] WARNING: Failed to overlay sequential number: {overlayEx.Message}");
                // Continue without overlay — photo is still usable
            }
            
            SessionService.SetFinalImage(outputPath);
            
            // Save session data as JSON
            // D4/P2: Include SequentialNumber in session.json for traceability
            var sessionData = new
            {
                DeviceId = DeviceConfig.DeviceId,
                LayoutUsed = layoutId ?? "unknown",
                FrameUsed = SessionService.CurrentSession.SelectedBackground?.Id ?? "unknown",
                SequentialNumber = SessionService.CurrentSession.SequentialNumber ?? "",
                PhotoCount = selectedPhotoPaths.Length,
                TotalCaptured = allPhotos.Count,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                FinalImagePath = outputPath
            };
            
            string jsonPath = Path.Combine(sessionDir, "session.json");
            string json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);
            Console.WriteLine($"[SESSION] Data saved: {jsonPath}");
            
            // Capture ALL session data as locals before fire-and-forget (Finding 8: avoid race with StartNewSession)
            var capturedDeviceId = DeviceConfig.DeviceId;
            var capturedStoreId = DeviceConfig.StoreId;
            var capturedFrameUsed = session.SelectedBackground?.Id ?? "unknown";
            var capturedApiBaseUrl = DeviceConfig.ApiBaseUrl;
            var capturedPhotoCount = selectedPhotoPaths.Length;
            var capturedTotalCaptured = allPhotos.Count;
            var capturedAmount = layoutId == "layout6" ? DeviceConfig.PriceLayout6 : DeviceConfig.PriceLayout2;

            // Send to API server (fire-and-forget with cancellation)
            _ = Task.Run(async () =>
            {
                try
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var apiData = new
                    {
                        DeviceId = capturedDeviceId,
                        StoreId = capturedStoreId,
                        LayoutUsed = layoutId ?? "unknown",
                        FrameUsed = capturedFrameUsed,
                        PhotoCount = capturedPhotoCount,
                        TotalCaptured = capturedTotalCaptured,
                        Amount = capturedAmount
                    };
                    var apiJson = JsonSerializer.Serialize(apiData);
                    
                    var httpClient = HttpService.Client;
                    // Finding 2: Dispose StringContent and HttpResponseMessage to prevent resource leaks
                    using var content = new System.Net.Http.StringContent(apiJson, System.Text.Encoding.UTF8, "application/json");
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                    linkedCts.CancelAfter(TimeSpan.FromSeconds(5));
                    using var response = await httpClient.PostAsync($"{capturedApiBaseUrl}/api/sessions", content, linkedCts.Token);
                    Console.WriteLine($"[API] Session sent: {response.StatusCode}");
                }
                catch (OperationCanceledException)
                {
                    // Expected when disposed before API call completes
                }
                catch (Exception apiEx)
                {
                    Console.WriteLine($"[API] Warning: Could not send to server - {apiEx.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to compose image: {ex.Message}");
        }
        finally
        {
            // Finding 5: Guaranteed cleanup of temp frame file even on exceptions
            if (tempFramePath != null)
            {
                try { File.Delete(tempFramePath); } catch { }
            }
        }
        
        NavigationService.NavigateTo<ConfirmPrintViewModel>();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo<CaptureViewModel>();
    }

    public void Dispose()
    {
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        BackgroundImage?.Dispose();
        BackgroundImage = null;
        FramePreviewImage?.Dispose();
        FramePreviewImage = null;
        
        foreach (var photo in Photos)
        {
            photo.Thumbnail?.Dispose();
            photo.Thumbnail = null;
        }
        
        // SelectedPhoto1..6 point to the same Thumbnail objects already disposed above
        SelectedPhoto1 = null;
        SelectedPhoto2 = null;
        SelectedPhoto3 = null;
        SelectedPhoto4 = null;
        SelectedPhoto5 = null;
        SelectedPhoto6 = null;
    }
}

public partial class PhotoItem : ObservableObject
{
    public int Index { get; set; }
    public string Path { get; set; } = string.Empty;
    public Bitmap? Thumbnail { get; set; }
    
    [ObservableProperty]
    private bool _isSelected;
}
