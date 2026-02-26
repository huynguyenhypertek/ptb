using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

public partial class FrameListViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    // Two separate collections for each layout
    [ObservableProperty]
    private ObservableCollection<FrameDisplayItem> _layout2Frames = new();

    [ObservableProperty]
    private ObservableCollection<FrameDisplayItem> _layout6Frames = new();

    [ObservableProperty]
    private ObservableCollection<StoreItem> _stores = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    // Which section is active: 0=layout2, 1=layout6
    [ObservableProperty]
    private int _activeTab;

    [ObservableProperty]
    private FrameDisplayItem? _selectedFrame;

    // Assignment panel
    [ObservableProperty]
    private bool _showAssignPanel;

    [ObservableProperty]
    private ObservableCollection<StoreAssignItem> _assignStores = new();

    public bool IsSystemAdmin => _apiService.Role == "SystemAdmin";

    // Computed: which tab is active
    public bool IsTab2Active => ActiveTab == 0;
    public bool IsTab6Active => ActiveTab == 1;

    partial void OnActiveTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTab2Active));
        OnPropertyChanged(nameof(IsTab6Active));
        SelectedFrame = null;
        ShowAssignPanel = false;
        StatusMessage = "";
    }

    public FrameListViewModel(ApiService apiService)
    {
        _apiService = apiService;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        await LoadStores();
        await LoadFrames();
    }

    private async Task LoadStores()
    {
        try
        {
            var stores = await _apiService.GetStoresAsync();
            if (stores != null)
            {
                Stores.Clear();
                foreach (var s in stores)
                    Stores.Add(s);
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task LoadFrames()
    {
        IsLoading = true;
        try
        {
            // Load layout2 frames
            var frames2 = await _apiService.GetFramesAsync("layout2");
            Layout2Frames.Clear();
            if (frames2 != null)
            {
                foreach (var f in frames2)
                    await AddFrameItem(f, Layout2Frames);
            }

            // Load layout6 frames
            var frames6 = await _apiService.GetFramesAsync("layout6");
            Layout6Frames.Clear();
            if (frames6 != null)
            {
                foreach (var f in frames6)
                    await AddFrameItem(f, Layout6Frames);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddFrameItem(FrameItem f, ObservableCollection<FrameDisplayItem> collection)
    {
        var item = new FrameDisplayItem
        {
            Id = f.Id,
            Name = f.Name,
            LayoutType = f.LayoutType,
            FileName = f.FileName,
            LayoutLabel = f.LayoutType == "layout2" ? "2 ảnh" : "6 ảnh"
        };

        // Load thumbnail
        var imageBytes = await _apiService.GetFrameImageAsync(f.Id);
        if (imageBytes != null)
        {
            using var ms = new MemoryStream(imageBytes);
            item.Thumbnail = new Bitmap(ms);
        }

        // Load assigned stores
        var assigned = await _apiService.GetAssignedStoresAsync(f.Id);
        if (assigned != null && assigned.Length > 0)
            item.AssignedStoresText = "🏪 " + string.Join(", ", assigned.Select(s => s.Name));
        else
            item.AssignedStoresText = "⬜ Chưa cấp cho cửa hàng nào";

        collection.Add(item);
    }

    [RelayCommand]
    private void SelectTab2()
    {
        ActiveTab = 0;
    }

    [RelayCommand]
    private void SelectTab6()
    {
        ActiveTab = 1;
    }

    [RelayCommand]
    private async Task SelectFrame(FrameDisplayItem? frame)
    {
        if (frame == null) return;
        
        // Deselect all
        foreach (var f in Layout2Frames) f.IsSelected = false;
        foreach (var f in Layout6Frames) f.IsSelected = false;
        
        frame.IsSelected = true;
        SelectedFrame = frame;
        StatusMessage = $"✅ Đã chọn: {frame.Name}";

        // If assign panel is open, refresh it for the new frame
        if (ShowAssignPanel)
            await ShowAssign();
    }

    [RelayCommand]
    private async Task ShowAssign()
    {
        if (SelectedFrame == null)
        {
            StatusMessage = "⚠️ Chọn 1 khung nền trước rồi bấm 'Cấp cho CH'";
            return;
        }

        AssignStores.Clear();
        var assigned = await _apiService.GetAssignedStoresAsync(SelectedFrame.Id);
        var assignedIds = assigned?.Select(s => s.Id).ToHashSet() ?? new();

        foreach (var store in Stores)
        {
            AssignStores.Add(new StoreAssignItem
            {
                StoreId = store.Id,
                StoreName = store.Name,
                IsAssigned = assignedIds.Contains(store.Id)
            });
        }

        ShowAssignPanel = true;
        StatusMessage = $"Cấp '{SelectedFrame.Name}' cho cửa hàng:";
    }

    [RelayCommand]
    private async Task AssignFrame(StoreAssignItem? item)
    {
        if (item == null || SelectedFrame == null) return;
        IsLoading = true;
        try
        {
            var error = await _apiService.AssignFrameToStoreAsync(SelectedFrame.Id, item.StoreId);
            if (error == null)
            {
                item.IsAssigned = true;
                StatusMessage = $"✅ Đã cấp '{SelectedFrame.Name}' cho {item.StoreName}";
                await LoadFrames();
            }
            else
            {
                StatusMessage = $"❌ {error}";
            }
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RevokeFrame(StoreAssignItem? item)
    {
        if (item == null || SelectedFrame == null) return;
        IsLoading = true;
        try
        {
            var success = await _apiService.RevokeFrameFromStoreAsync(SelectedFrame.Id, item.StoreId);
            if (success)
            {
                item.IsAssigned = false;
                StatusMessage = $"✅ Đã thu hồi '{SelectedFrame.Name}' khỏi {item.StoreName}";
                await LoadFrames();
            }
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void CloseAssignPanel()
    {
        ShowAssignPanel = false;
        StatusMessage = "";
    }

    [RelayCommand]
    private async Task UploadFrame()
    {
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Chọn file khung nền",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Images")
                        { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } }
                    }
                });

            if (files.Count == 0) return;

            var file = files[0];
            var filePath = file.Path.LocalPath;
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            
            // Layout type based on active tab
            string layoutType = ActiveTab == 1 ? "layout6" : "layout2";

            IsLoading = true;
            StatusMessage = "⏳ Đang upload...";
            
            var success = await _apiService.UploadFrameAsync(filePath, fileName, layoutType);
            if (success)
            {
                StatusMessage = $"✅ Upload thành công vào mục {(layoutType == "layout2" ? "2 ảnh" : "6 ảnh")}!";
                await LoadFrames();
            }
            else StatusMessage = "❌ Lỗi upload";
        }
        catch (Exception ex) { StatusMessage = $"❌ Lỗi: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task DeleteFrame()
    {
        if (SelectedFrame == null)
        {
            StatusMessage = "⚠️ Chọn khung nền trước";
            return;
        }
        IsLoading = true;
        try
        {
            var success = await _apiService.DeleteFrameAsync(SelectedFrame.Id);
            if (success)
            {
                StatusMessage = $"✅ Đã xóa {SelectedFrame.Name}";
                SelectedFrame = null;
                await LoadFrames();
            }
            else StatusMessage = "❌ Lỗi xóa";
        }
        catch (Exception ex) { StatusMessage = $"❌ Lỗi: {ex.Message}"; }
        finally { IsLoading = false; }
    }
}

public partial class FrameDisplayItem : ObservableObject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LayoutType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string LayoutLabel { get; set; } = "";
    
    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private string _assignedStoresText = "";

    [ObservableProperty]
    private bool _isSelected;
}

public partial class StoreAssignItem : ObservableObject
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = "";

    [ObservableProperty]
    private bool _isAssigned;
}
