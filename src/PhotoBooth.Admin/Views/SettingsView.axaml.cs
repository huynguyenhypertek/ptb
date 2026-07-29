using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PhotoBooth.Admin.ViewModels;

namespace PhotoBooth.Admin.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        // Subscribe to ViewModel's folder browse request
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.BrowseFolderRequested += OnBrowseFolderRequested;
            }
        };
    }

    private async void OnBrowseFolderRequested()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Chọn thư mục Google Drive",
                AllowMultiple = false
            });

        if (folders.Count > 0)
        {
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path) && DataContext is SettingsViewModel vm)
            {
                vm.SetGoogleDrivePath(path);
            }
        }
    }
}
