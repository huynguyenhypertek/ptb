using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

// Represents a Store group with its admin and devices
public partial class StoreGroup : ObservableObject
{
    public StoreItem Store { get; set; } = new();
    public UserItem? StoreAdmin { get; set; }
    public ObservableCollection<UserItem> Devices { get; set; } = new();
    
    [ObservableProperty]
    private bool _isExpanded;
    
    public string DisplayName => $"🏪 {Store.Name}";
    public string AdminName => StoreAdmin != null ? $"👤 Admin: {StoreAdmin.Username}" : "⚠️ Chưa có admin";
    public string DeviceCount => $"📷 {Devices.Count} máy chụp";
}

public partial class UserListViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    
    [ObservableProperty]
    private ObservableCollection<UserItem> _users = new();

    [ObservableProperty]
    private ObservableCollection<StoreItem> _stores = new();

    [ObservableProperty]
    private ObservableCollection<StoreGroup> _storeGroups = new();

    [ObservableProperty]
    private bool _isLoading;

    // Form fields
    [ObservableProperty]
    private bool _showForm;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private int _editingUserId;

    [ObservableProperty]
    private string _formUsername = "";

    [ObservableProperty]
    private string _formPassword = "";

    [ObservableProperty]
    private int _formRoleIndex;  // always 1=Device (StoreAdmin created in Store module)

    [ObservableProperty]
    private int _formStoreIndex;

    [ObservableProperty]
    private string _formStoreName = "";  // For new store when StoreAdmin

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private UserItem? _selectedUser;

    // Role-based visibility
    public bool IsSystemAdmin => _apiService.Role == "SystemAdmin";
    public bool IsStoreAdmin => _apiService.Role == "StoreAdmin";

    // Form: show dropdown or text input based on selected role
    public bool IsRoleDevice => FormRoleIndex == 1;
    public bool IsRoleStoreAdmin => FormRoleIndex == 0;

    partial void OnFormRoleIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsRoleDevice));
        OnPropertyChanged(nameof(IsRoleStoreAdmin));
    }

    public UserListViewModel(ApiService apiService)
    {
        _apiService = apiService;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        await LoadStores();
        await LoadUsers();
    }

    [RelayCommand]
    private async Task LoadUsers()
    {
        IsLoading = true;
        try
        {
            var users = await _apiService.GetUsersAsync(
                _apiService.Role == "StoreAdmin" ? _apiService.StoreId : null
            );
            if (users != null)
            {
                Users.Clear();
                foreach (var u in users)
                    Users.Add(u);
                
                // Build hierarchical groups for SystemAdmin
                if (IsSystemAdmin)
                    BuildStoreGroups();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi tải danh sách: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildStoreGroups()
    {
        StoreGroups.Clear();
        
        foreach (var store in Stores)
        {
            var group = new StoreGroup { Store = store };
            
            // Find StoreAdmin for this store
            group.StoreAdmin = Users.FirstOrDefault(u => u.Role == "StoreAdmin" && u.StoreId == store.Id);
            
            // Find Devices for this store
            foreach (var device in Users.Where(u => u.Role == "Device" && u.StoreId == store.Id))
                group.Devices.Add(device);
            
            StoreGroups.Add(group);
        }
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
    private void ShowCreateForm()
    {
        IsEditing = false;
        EditingUserId = 0;
        FormUsername = "";
        FormPassword = "";
        FormRoleIndex = 1; // Always Device
        FormStoreIndex = 0;
        FormStoreName = "";
        StatusMessage = "";
        ShowForm = true;
    }

    [RelayCommand]
    private void EditUser(UserItem? user)
    {
        if (user == null) return;
        
        IsEditing = true;
        EditingUserId = user.Id;
        FormUsername = user.Username;
        FormPassword = "";
        FormRoleIndex = user.Role == "Device" ? 1 : 0;
        
        FormStoreIndex = 0;
        if (user.StoreId.HasValue)
        {
            for (int i = 0; i < Stores.Count; i++)
            {
                if (Stores[i].Id == user.StoreId.Value)
                {
                    FormStoreIndex = i;
                    break;
                }
            }
        }
        StatusMessage = "";
        ShowForm = true;
    }

    [RelayCommand]
    private async Task SaveUser()
    {
        if (string.IsNullOrWhiteSpace(FormUsername))
        {
            StatusMessage = "❌ Vui lòng nhập username";
            return;
        }

        if (!IsEditing && string.IsNullOrWhiteSpace(FormPassword))
        {
            StatusMessage = "❌ Vui lòng nhập mật khẩu";
            return;
        }

        IsLoading = true;
        try
        {
            string role = "Device"; // Always Device (StoreAdmin created in Store module)
            int? storeId = null;

            if (IsStoreAdmin)
            {
                storeId = _apiService.StoreId;
            }
            else
            {
                // SystemAdmin: pick existing store from dropdown
                storeId = Stores.Count > FormStoreIndex ? Stores[FormStoreIndex].Id : null;
            }

            bool success;
            if (IsEditing)
            {
                string? password = string.IsNullOrWhiteSpace(FormPassword) ? null : FormPassword;
                success = await _apiService.UpdateUserAsync(EditingUserId, password, role, storeId);
                StatusMessage = success ? "✅ Đã cập nhật!" : "❌ Lỗi cập nhật";
            }
            else
            {
                success = await _apiService.CreateUserAsync(FormUsername, FormPassword, role, storeId);
                StatusMessage = success ? "✅ Đã tạo tài khoản!" : "❌ Username đã tồn tại";
            }

            if (success)
            {
                ShowForm = false;
                await LoadUsers();
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
    private async Task DeleteUser(UserItem? user)
    {
        if (user == null) return;
        
        if (user.Username == _apiService.Username)
        {
            StatusMessage = "❌ Không thể xóa tài khoản đang đăng nhập!";
            return;
        }

        IsLoading = true;
        try
        {
            var success = await _apiService.DeleteUserAsync(user.Id);
            if (success)
            {
                StatusMessage = $"✅ Đã xóa {user.Username}";
                await LoadUsers();
            }
            else StatusMessage = "❌ Lỗi xóa user";
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
    private async Task ToggleUserEnabled(UserItem? user)
    {
        if (user == null) return;
        IsLoading = true;
        try
        {
            var success = await _apiService.ToggleUserEnabledAsync(user.Id);
            if (success)
            {
                var newStatus = user.IsEnabled ? "🔴 Ngưng" : "🟢 Kích hoạt";
                StatusMessage = $"✅ {user.Username} → {newStatus}";
                await LoadUsers();
            }
            else StatusMessage = "❌ Lỗi thay đổi trạng thái";
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
