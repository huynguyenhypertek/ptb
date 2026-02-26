using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

public partial class StoreListViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<StoreItem> _stores = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private StoreItem? _selectedStore;

    // Form mode: false=create, true=edit
    [ObservableProperty]
    private bool _showForm;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _formName = "";

    [ObservableProperty]
    private string _formAddress = "";

    [ObservableProperty]
    private int _formPlanIndex;  // 0=Basic, 1=Pro

    // Create-only fields (admin account)
    [ObservableProperty]
    private string _formAdminUsername = "";

    [ObservableProperty]
    private string _formAdminPassword = "";

    [ObservableProperty]
    private string _statusMessage = "";

    public StoreListViewModel(ApiService apiService)
    {
        _apiService = apiService;
        LoadStoresCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadStores()
    {
        IsLoading = true;
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
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowCreateForm()
    {
        IsEditing = false;
        FormName = "";
        FormAddress = "";
        FormPlanIndex = 0;
        FormAdminUsername = "";
        FormAdminPassword = "";
        StatusMessage = "";
        ShowForm = true;
    }

    [RelayCommand]
    private void EditStore()
    {
        if (SelectedStore == null)
        {
            StatusMessage = "⚠️ Chọn cửa hàng trước";
            return;
        }
        IsEditing = true;
        FormName = SelectedStore.Name;
        FormAddress = SelectedStore.Address;
        FormPlanIndex = SelectedStore.PlanType == "Pro" ? 1 : 0;
        StatusMessage = "";
        ShowForm = true;
    }

    [RelayCommand]
    private async Task SaveStore()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            StatusMessage = "❌ Vui lòng nhập tên cửa hàng";
            return;
        }

        IsLoading = true;
        try
        {
            var planType = FormPlanIndex == 1 ? "Pro" : "Basic";

            if (IsEditing)
            {
                // Update existing store
                if (SelectedStore == null) return;
                var success = await _apiService.UpdateStoreAsync(SelectedStore.Id, FormName, FormAddress, planType);
                if (success)
                {
                    StatusMessage = "✅ Đã cập nhật cửa hàng!";
                    ShowForm = false;
                    await LoadStores();
                }
                else StatusMessage = "❌ Lỗi cập nhật";
            }
            else
            {
                // Create new store + admin account
                if (string.IsNullOrWhiteSpace(FormAdminUsername))
                {
                    StatusMessage = "❌ Vui lòng nhập tài khoản admin cửa hàng";
                    IsLoading = false;
                    return;
                }
                if (string.IsNullOrWhiteSpace(FormAdminPassword))
                {
                    StatusMessage = "❌ Vui lòng nhập mật khẩu admin cửa hàng";
                    IsLoading = false;
                    return;
                }

                // Step 1: Create store
                var storeId = await _apiService.CreateStoreAsync(FormName, FormAddress);
                if (storeId == null)
                {
                    StatusMessage = "❌ Lỗi tạo cửa hàng";
                    IsLoading = false;
                    return;
                }

                // Step 2: Create admin account for this store
                var success = await _apiService.CreateUserAsync(FormAdminUsername, FormAdminPassword, "StoreAdmin", storeId);
                if (success)
                {
                    StatusMessage = $"✅ Đã tạo cửa hàng '{FormName}' + admin '{FormAdminUsername}'!";
                    ShowForm = false;
                    await LoadStores();
                }
                else
                {
                    StatusMessage = "❌ Tạo cửa hàng OK nhưng lỗi tạo admin (username đã tồn tại?)";
                }
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

    [RelayCommand]
    private void CancelForm()
    {
        ShowForm = false;
        StatusMessage = "";
    }
}
