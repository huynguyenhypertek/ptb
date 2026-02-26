using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.ViewModels;

public partial class PlanListViewModel : ObservableObject
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<PlanItem> _plans = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private PlanItem? _selectedPlan;

    // Form
    [ObservableProperty]
    private bool _showForm;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private int _editingId;

    [ObservableProperty]
    private string _formName = "";

    [ObservableProperty]
    private string _formDescription = "";

    [ObservableProperty]
    private string _formPrice = "";

    [ObservableProperty]
    private string _formMaxDevices = "";

    [ObservableProperty]
    private string _formMaxPhotos = "";

    [ObservableProperty]
    private bool _formHasFrames;

    [ObservableProperty]
    private bool _formHasAnalytics;

    [ObservableProperty]
    private bool _formIsActive = true;

    public PlanListViewModel(ApiService apiService)
    {
        _apiService = apiService;
        _ = LoadPlans();
    }

    [RelayCommand]
    private async Task LoadPlans()
    {
        IsLoading = true;
        try
        {
            var plans = await _apiService.GetPlansAsync();
            if (plans != null)
            {
                Plans.Clear();
                foreach (var p in plans)
                    Plans.Add(p);
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
        EditingId = 0;
        FormName = "";
        FormDescription = "";
        FormPrice = "";
        FormMaxDevices = "";
        FormMaxPhotos = "";
        FormHasFrames = false;
        FormHasAnalytics = false;
        FormIsActive = true;
        StatusMessage = "";
        ShowForm = true;
    }

    [RelayCommand]
    private void EditPlan()
    {
        if (SelectedPlan == null)
        {
            StatusMessage = "⚠️ Chọn gói trước";
            return;
        }
        IsEditing = true;
        EditingId = SelectedPlan.Id;
        FormName = SelectedPlan.Name;
        FormDescription = SelectedPlan.Description;
        FormPrice = SelectedPlan.Price.ToString("0");
        FormMaxDevices = SelectedPlan.MaxDevices.ToString();
        FormMaxPhotos = SelectedPlan.MaxPhotosPerDay.ToString();
        FormHasFrames = SelectedPlan.HasCustomFrames;
        FormHasAnalytics = SelectedPlan.HasAnalytics;
        FormIsActive = SelectedPlan.IsActive;
        StatusMessage = "";
        ShowForm = true;
    }

    [RelayCommand]
    private async Task SavePlan()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            StatusMessage = "❌ Nhập tên gói";
            return;
        }

        if (!decimal.TryParse(FormPrice, out var price))
        {
            StatusMessage = "❌ Giá không hợp lệ";
            return;
        }

        int.TryParse(FormMaxDevices, out var maxDevices);
        int.TryParse(FormMaxPhotos, out var maxPhotos);

        IsLoading = true;
        try
        {
            var plan = new PlanItem
            {
                Name = FormName,
                Description = FormDescription,
                Price = price,
                MaxDevices = maxDevices,
                MaxPhotosPerDay = maxPhotos,
                HasCustomFrames = FormHasFrames,
                HasAnalytics = FormHasAnalytics,
                IsActive = FormIsActive
            };

            bool success;
            if (IsEditing)
            {
                success = await _apiService.UpdatePlanAsync(EditingId, plan);
                StatusMessage = success ? "✅ Đã cập nhật!" : "❌ Lỗi cập nhật";
            }
            else
            {
                success = await _apiService.CreatePlanAsync(plan);
                StatusMessage = success ? "✅ Đã tạo gói mới!" : "❌ Lỗi tạo gói";
            }

            if (success)
            {
                ShowForm = false;
                await LoadPlans();
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
    private async Task DeletePlan()
    {
        if (SelectedPlan == null)
        {
            StatusMessage = "⚠️ Chọn gói trước";
            return;
        }

        IsLoading = true;
        try
        {
            var success = await _apiService.DeletePlanAsync(SelectedPlan.Id);
            if (success)
            {
                StatusMessage = $"✅ Đã xóa {SelectedPlan.Name}";
                await LoadPlans();
            }
            else StatusMessage = "❌ Lỗi xóa";
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
