using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhotoBooth.Admin.Services;

public class ApiService
{
    private readonly HttpClient _client;
    public string BaseUrl { get; set; } = "http://localhost:5148";
    
    // Stored after login
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public int? StoreId { get; set; }
    public string? StoreName { get; set; }

    public ApiService()
    {
        _client = new HttpClient();
        _client.Timeout = TimeSpan.FromSeconds(10);
    }

    // Login
    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/api/auth/login",
            new { Username = username, Password = password });
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result != null)
            {
                Token = result.Token;
                Username = result.Username;
                Role = result.Role;
                StoreId = result.StoreId;
                StoreName = result.StoreName;
                
                // Set JWT Authorization header for all subsequent requests
                _client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
            }
            return result;
        }
        return null;
    }

    // Sessions
    public async Task<SessionItem[]?> GetSessionsAsync()
    {
        var url = $"{BaseUrl}/api/sessions";
        if (Role == "StoreAdmin" && StoreId.HasValue)
            url += $"?storeId={StoreId.Value}";
        return await _client.GetFromJsonAsync<SessionItem[]>(url);
    }

    public async Task<StatsResponse?> GetStatsAsync()
    {
        var url = $"{BaseUrl}/api/sessions/stats";
        if (Role == "StoreAdmin" && StoreId.HasValue)
            url += $"?storeId={StoreId.Value}";
        return await _client.GetFromJsonAsync<StatsResponse>(url);
    }

    // Stores
    public async Task<StoreItem[]?> GetStoresAsync()
    {
        return await _client.GetFromJsonAsync<StoreItem[]>($"{BaseUrl}/api/stores");
    }

    public async Task<bool> UpdateStoreAsync(int id, string name, string address, string planType = "Basic")
    {
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/api/stores/{id}",
            new { Name = name, Address = address, PlanType = planType });
        return response.IsSuccessStatusCode;
    }

    public async Task<int?> CreateStoreAsync(string name, string address = "")
    {
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/api/stores",
            new { Name = name, Address = address, PlanType = "Basic" });
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("id").GetInt32();
        }
        return null;
    }

    // Users - CRUD
    public async Task<UserItem[]?> GetUsersAsync(int? storeId = null)
    {
        var url = $"{BaseUrl}/api/users";
        if (storeId.HasValue)
            url += $"?storeId={storeId.Value}";
        return await _client.GetFromJsonAsync<UserItem[]>(url);
    }

    public async Task<bool> CreateUserAsync(string username, string password, string role, int? storeId)
    {
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/api/users",
            new { Username = username, Password = password, Role = role, StoreId = storeId });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateUserAsync(int id, string? password, string role, int? storeId)
    {
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/api/users/{id}",
            new { Password = password, Role = role, StoreId = storeId });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var response = await _client.DeleteAsync($"{BaseUrl}/api/users/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ToggleUserEnabledAsync(int id)
    {
        var response = await _client.PutAsync($"{BaseUrl}/api/users/{id}/toggle", null);
        return response.IsSuccessStatusCode;
    }

    // Frames
    public async Task<FrameItem[]?> GetFramesAsync(string? layoutType = null)
    {
        var url = $"{BaseUrl}/api/frames";
        if (!string.IsNullOrEmpty(layoutType))
            url += $"?layoutType={layoutType}";
        return await _client.GetFromJsonAsync<FrameItem[]>(url);
    }

    public async Task<byte[]?> GetFrameImageAsync(int id)
    {
        try
        {
            return await _client.GetByteArrayAsync($"{BaseUrl}/api/frames/{id}/image");
        }
        catch { return null; }
    }

    public string GetFrameImageUrl(int id) => $"{BaseUrl}/api/frames/{id}/image";

    public async Task<bool> UploadFrameAsync(string filePath, string name, string layoutType)
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var fileContent = new ByteArrayContent(fileBytes);
        
        // Fix: Set correct Content-Type so API FileValidationHelper doesn't reject it
        var ext = Path.GetExtension(filePath).ToLower();
        if (ext == ".jpg" || ext == ".jpeg") 
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        else if (ext == ".png") 
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        content.Add(fileContent, "file", Path.GetFileName(filePath));
        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(layoutType), "layoutType");
        
        var response = await _client.PostAsync($"{BaseUrl}/api/frames", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteFrameAsync(int id)
    {
        var response = await _client.DeleteAsync($"{BaseUrl}/api/frames/{id}");
        return response.IsSuccessStatusCode;
    }

    // Frame-Store Assignment
    public async Task<FrameItem[]?> GetFramesByStoreAsync(int storeId, string? layoutType = null)
    {
        var url = $"{BaseUrl}/api/frames/store/{storeId}";
        if (!string.IsNullOrEmpty(layoutType))
            url += $"?layoutType={layoutType}";
        return await _client.GetFromJsonAsync<FrameItem[]>(url);
    }

    public async Task<AssignedStore[]?> GetAssignedStoresAsync(int frameId)
    {
        return await _client.GetFromJsonAsync<AssignedStore[]>($"{BaseUrl}/api/frames/{frameId}/stores");
    }

    public async Task<string?> AssignFrameToStoreAsync(int frameId, int storeId)
    {
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/api/frames/assign",
            new { FrameId = frameId, StoreId = storeId });
        if (response.IsSuccessStatusCode) return null; // success
        // Return error message from API
        try
        {
            var err = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return err.GetProperty("message").GetString() ?? "Lỗi cấp khung";
        }
        catch { return "Lỗi cấp khung"; }
    }

    public async Task<bool> RevokeFrameFromStoreAsync(int frameId, int storeId)
    {
        var response = await _client.DeleteAsync($"{BaseUrl}/api/frames/revoke?frameId={frameId}&storeId={storeId}");
        return response.IsSuccessStatusCode;
    }

    // Subscription Plans
    public async Task<PlanItem[]?> GetPlansAsync()
    {
        return await _client.GetFromJsonAsync<PlanItem[]>($"{BaseUrl}/api/subscriptionplans");
    }

    public async Task<bool> CreatePlanAsync(PlanItem plan)
    {
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/api/subscriptionplans", plan);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdatePlanAsync(int id, PlanItem plan)
    {
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/api/subscriptionplans/{id}", plan);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeletePlanAsync(int id)
    {
        var response = await _client.DeleteAsync($"{BaseUrl}/api/subscriptionplans/{id}");
        return response.IsSuccessStatusCode;
    }
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public int? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string PlanType { get; set; } = "Basic";
}

public class SessionItem
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = "";
    public int? StoreId { get; set; }
    public string LayoutUsed { get; set; } = "";
    public string FrameUsed { get; set; } = "";
    public int PhotoCount { get; set; }
    public int TotalCaptured { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StatsResponse
{
    public int TotalSessions { get; set; }
    public int TodaySessions { get; set; }
    public int TotalPhotos { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TodayRevenue { get; set; }
}

public class StoreItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string PlanType { get; set; } = "Basic";
    public DateTime CreatedAt { get; set; }
}

public class UserItem
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public int? StoreId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class FrameItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LayoutType { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class PlanItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int MaxDevices { get; set; }
    public int MaxPhotosPerDay { get; set; }
    public bool HasCustomFrames { get; set; }
    public bool HasAnalytics { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AssignedStore
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

