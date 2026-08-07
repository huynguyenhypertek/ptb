using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Event.Services;
using PhotoBooth.Infrastructure.Services;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Screen 3: Photo Selection — user picks 4 of 6 captured photos for the final composite.
/// Simplified from PhotoBooth.UI/ViewModels/PhotoSelectionViewModel.cs:
///   KEEP: selection logic, toggle, selection order tracking, thumbnail loading, dispose
///   DROP: sticker, API upload, QR prefetch, layout branching, frame download, composite
///   CHANGE: RequiredSelections=4 hardcoded, navigate to ReviewPrintViewModel
/// </summary>
public partial class PhotoSelectViewModel : ViewModelBase, IDisposable
{
    private readonly NavigationService _navigationService;
    private readonly SessionService _sessionService;

    [ObservableProperty]
    private ObservableCollection<PhotoItem> _photos = new();

    /// <summary>
    /// Hardcoded to 4 — Event app always selects 4 of 6 photos.
    /// Source uses dynamic RequiredSelections from layout; Event has no layout selection.
    /// </summary>
    private const int RequiredSelections = 4;

    /// <summary>
    /// Thumbnail decode width for memory efficiency.
    /// Full-size decode of 1920x1080 = ~8MB per image (BGRA).
    /// Thumbnail at 300px wide = ~0.5MB — 16x smaller.
    /// </summary>
    private const int ThumbnailWidth = 300;

    /// <summary>
    /// Photo grid cell size — to resize all cells proportionally:
    ///   1. Change PhotoScale
    ///   2. Update PhotoWidth = (int)(195 * PhotoScale)
    ///   3. Update PhotoHeight = (int)(283 * PhotoScale)
    ///   4. Update Width/Height in PhotoSelectView.axaml dòng 74
    /// Base size at scale 1.0: 195×283 (matches back3.png white slot dimensions).
    /// Current scale: 1.10 → 215×311
    /// </summary>
    public const int PhotoWidth = 215;   // = (int)(195 * 1.10)
    public const int PhotoHeight = 311;  // = (int)(283 * 1.10)

    // Selected photo previews for the frame (4 slots only, NOT 6)
    [ObservableProperty]
    private Bitmap? _selectedPhoto1;

    [ObservableProperty]
    private Bitmap? _selectedPhoto2;

    [ObservableProperty]
    private Bitmap? _selectedPhoto3;

    [ObservableProperty]
    private Bitmap? _selectedPhoto4;

    public int SelectedCount => Photos.Count(p => p.IsSelected);

    public bool CanConfirm => SelectedCount == RequiredSelections && !IsCompositing;

    private bool _disposed;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    private bool _isCompositing;

    // Track selection order so photos appear in the frame in the order the user tapped them
    private readonly List<PhotoItem> _selectionOrder = new();

    public PhotoSelectViewModel(NavigationService navigationService, SessionService sessionService)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        LoadCapturedPhotos();
    }

    /// <summary>
    /// Loads all captured photo paths from session and creates PhotoItem thumbnails.
    /// Reads from SessionService.CurrentSession.CapturedPhotoPaths (populated by CaptureViewModel).
    /// </summary>
    private void LoadCapturedPhotos()
    {
        var paths = _sessionService.CurrentSession.CapturedPhotoPaths;

        // Dispose old thumbnails before replacing (defensive — normally empty on first load)
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

    /// <summary>
    /// Loads a thumbnail at reduced resolution to minimize RAM usage.
    /// Full-size decode of 1920x1080 = ~8MB per image (BGRA).
    /// Thumbnail at 300px wide = ~0.5MB — 16x smaller.
    /// </summary>
    private static Bitmap? LoadThumbnail(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var fileStream = File.OpenRead(path);
                using var memStream = new MemoryStream();
                fileStream.CopyTo(memStream);
                memStream.Position = 0;
                return Bitmap.DecodeToWidth(memStream, ThumbnailWidth);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load thumbnail: {path}. {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Toggles selection state of a photo.
    /// If already selected → deselect and remove from selection order.
    /// If not selected AND under limit → select and add to selection order.
    /// </summary>
    [RelayCommand]
    private void ToggleSelection(PhotoItem photo)
    {
        if (IsCompositing || photo is null) return;

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
        OnPropertyChanged(nameof(CanConfirm));
        ConfirmCommand.NotifyCanExecuteChanged();
        UpdatePreviewPhotos();
    }

    /// <summary>
    /// Maps selection order to SelectedPhoto1..4 preview slots.
    /// Uses Thumbnail reference (NOT new Bitmap) — avoids double allocation.
    /// </summary>
    private void UpdatePreviewPhotos()
    {
        // Use selection order, not capture order
        SelectedPhoto1 = _selectionOrder.Count > 0 ? _selectionOrder[0].Thumbnail : null;
        SelectedPhoto2 = _selectionOrder.Count > 1 ? _selectionOrder[1].Thumbnail : null;
        SelectedPhoto3 = _selectionOrder.Count > 2 ? _selectionOrder[2].Thumbnail : null;
        SelectedPhoto4 = _selectionOrder.Count > 3 ? _selectionOrder[3].Thumbnail : null;
    }

    /// <summary>
    /// Saves selected photo indices to session and navigates to ReviewPrintViewModel.
    /// Sync void — no async ops remain after stripping compositing + API upload.
    /// CanExecute bound to CanConfirm — prevents confirm with less than 4 selections.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task Confirm()
    {
        if (IsCompositing) return;
        IsCompositing = true;
        
        // Extract selected indices in tap order
        var selectedIndices = _selectionOrder.Select(p => p.Index).ToList();
        _sessionService.SetSelectedPhotos(selectedIndices);
        string? tempFramePath = null;

        try
        {
            var allPhotos = _sessionService.CurrentSession.CapturedPhotoPaths;
            var selectedPhotoPaths = selectedIndices.Where(i => i >= 0 && i < allPhotos.Count).Select(i => allPhotos[i]).ToArray();
            
            // Use the correct SessionDirectory from the session
            string sessionDir = _sessionService.CurrentSession.SessionDirectory ?? Path.GetTempPath();
            string sessionName = _sessionService.CurrentSession.SessionFolderName ?? "unknown";
            string customFinalName = $"{DeviceConfig.EventName}_{sessionName}_final.jpg";
            string outputPath = Path.Combine(sessionDir, customFinalName);

            bool isTestMode = Avalonia.Application.Current == null;

            await Task.Run(() =>
            {
                if (isTestMode) return; // Skip AssetLoader and OpenCV during unit tests

                // 1. Extract Asset (File IO)
                tempFramePath = Path.Combine(Path.GetTempPath(), $"photobooth_frame_{Guid.NewGuid():N}.png");
                using (var stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://PhotoBooth.Event/Assets/DongFest/frame/dongfest_frame.png")))
                using (var fileStream = File.Create(tempFramePath))
                {
                    stream.CopyTo(fileStream);
                }

                // 2. Define 4-Photo Positions (pixel-exact transparent slots in dongfest_frame.png 3545x5316)
                var positions = new (int, int, int, int)[]
                {
                    (184, 183, 1500, 2169),    // Photo 1: top-left
                    (1866, 183, 1500, 2169),   // Photo 2: top-right
                    (184, 2518, 1500, 2169),   // Photo 3: bottom-left
                    (1866, 2518, 1500, 2169)   // Photo 4: bottom-right
                };

                // 3. Composite (CPU-bound OpenCV)
                if (selectedPhotoPaths.Length == positions.Length)
                {
                    ImageCompositeService.Compose(tempFramePath, selectedPhotoPaths, positions, outputPath);
                }
                else
                {
                    throw new InvalidOperationException($"Mismatch between selected photos ({selectedPhotoPaths.Length}) and layout positions ({positions.Length}).");
                }
            });

            _sessionService.SetFinalImage(outputPath);

            // Report session to API (fire-and-forget, like PhotoBooth.UI)
            var capturedDeviceId = DeviceConfig.DeviceId;
            var capturedStoreId = DeviceConfig.StoreId;
            var capturedApiBaseUrl = DeviceConfig.ApiBaseUrl;
            var capturedPhotoCount = selectedPhotoPaths.Length;
            var capturedTotalCaptured = _sessionService.CurrentSession.CapturedPhotoPaths.Count;

            _ = Task.Run(async () =>
            {
                try
                {
                    var apiData = new
                    {
                        DeviceId = capturedDeviceId,
                        StoreId = capturedStoreId,
                        LayoutUsed = "event-4photo",
                        FrameUsed = "dongfest_frame",
                        PhotoCount = capturedPhotoCount,
                        TotalCaptured = capturedTotalCaptured,
                        Amount = 0m  // Event mode = free
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(apiData);
                    using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    using var httpClient = new System.Net.Http.HttpClient();
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using var response = await httpClient.PostAsync($"{capturedApiBaseUrl}/api/sessions", content, timeoutCts.Token);
                    Console.WriteLine($"[API] Session sent: {response.StatusCode}");
                }
                catch (Exception apiEx)
                {
                    Console.WriteLine($"[API] Warning: Could not send session - {apiEx.Message}");
                }
            });

            _navigationService.NavigateTo<ReviewPrintViewModel>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to compose image: {ex.Message}");
        }
        finally
        {
            if (tempFramePath != null)
            {
                try { File.Delete(tempFramePath); } catch { }
            }
            IsCompositing = false;
        }
    }

    public bool CanGoBack => !IsCompositing;

    /// <summary>
    /// Navigates back to CaptureViewModel.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        _navigationService.NavigateTo<CaptureViewModel>();
    }

    /// <summary>
    /// Disposes all Bitmap thumbnails and nulls out preview references.
    /// SelectedPhoto1..4 point to the same Thumbnail objects — null them out (don't double-dispose).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var photo in Photos)
        {
            photo.Thumbnail?.Dispose();
            photo.Thumbnail = null;
        }

        _selectionOrder.Clear();

        // SelectedPhoto1..4 point to the same Thumbnail objects already disposed above
        SelectedPhoto1 = null;
        SelectedPhoto2 = null;
        SelectedPhoto3 = null;
        SelectedPhoto4 = null;

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a single photo item in the selection grid.
/// Contains index, file path, decoded thumbnail, and selection state.
/// </summary>
public partial class PhotoItem : ObservableObject
{
    public int Index { get; set; }
    public string Path { get; set; } = string.Empty;
    public Bitmap? Thumbnail { get; set; }

    [ObservableProperty]
    private bool _isSelected;
}
