using System.IO;

namespace PhotoBooth.UI;

/// <summary>
/// Stores device info passed from Admin app login (via command-line args)
/// </summary>
public static class DeviceConfig
{
    public static int? StoreId { get; set; }
    public static string DeviceId { get; set; } = "device-1";
    public static string PlanType { get; set; } = "Pro";  // "Basic" or "Pro"
    public static decimal PriceLayout6 { get; set; } = 70000m;
    public static decimal PriceLayout2 { get; set; } = 50000m;
    private static string _apiBaseUrl = "https://intellective-unimpinging-greyson.ngrok-free.dev";
    public static string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => _apiBaseUrl = value?.TrimEnd('/') ?? "";
    }
    // ====================================================================
    // CẤU HÌNH GOOGLE DRIVE — SỬA Ở ĐÂY
    // ====================================================================
    //
    // 🔧 Bật/tắt Google Drive:
    //    true  = ảnh lưu vào Google Drive (dùng GoogleDrivePath bên dưới)
    //    false = ảnh lưu vào ~/Pictures/PhotoBooth/ (mặc định)
    //
    public static bool GoogleDriveEnabled { get; set; } = true;

    // 🔧 Đường dẫn folder trên máy tính (Google Drive Desktop sync):
    //    - Đây là folder MÀ ẢNH SẼ ĐƯỢC LƯU VÀO
    //    - App sẽ tạo subfolder session bên trong folder này
    //    - Folder này PHẢI tồn tại trên máy (Google Drive Desktop đang chạy)
    //
    //    ⚠️ QUAN TRỌNG: Tên folder cuối cùng ("PhotoBooth") phải TRÙNG với
    //    tên folder trong Apps Script (Code.gs). Nếu đổi tên ở đây,
    //    phải vào Apps Script đổi dòng:
    //      var parents = DriveApp.getFoldersByName("PhotoBooth");
    //    thành tên mới tương ứng.
    //
    public static string GoogleDrivePath { get; set; } = "/Users/huynguyen/Library/CloudStorage/GoogleDrive-dhgaming12th4@gmail.com/Drive của tôi/sending/PhotoBooth";

    // 🔧 URL Apps Script (lấy từ Google Drive → Apps Script → Deploy):
    //    - URL này cố định, KHÔNG đổi trừ khi bạn tạo deployment mới
    //    - Nếu cần sửa code Apps Script: Triển khai → Quản lý → Chỉnh sửa → Phiên bản mới
    //
    public static string AppsScriptUrl { get; set; } = "https://script.google.com/macros/s/AKfycbxNiDN4DpVJswsasmaxUpubSSvI2ATO9PI_ZLbeRxl0bhX8VZF_g-FgR3SYj0MkyJjawQ/exec";

    /// <summary>
    /// Returns GoogleDrivePath with PII masked — shows only last 2 path segments.
    /// Use this in all log output to avoid leaking user email from Google Drive paths.
    /// </summary>
    public static string GetMaskedGDrivePath()
    {
        if (string.IsNullOrEmpty(GoogleDrivePath)) return "";
        var segments = GoogleDrivePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // F5: Guard against short paths (e.g. "/" or "C:") to prevent ArgumentOutOfRangeException
        if (segments.Length < 2) return ".../";
        return ".../" + string.Join("/", segments[^2..]);
    }

    /// <summary>
    /// Returns AppsScriptUrl with deployment key masked — shows only domain + first 10 chars.
    /// Deployment keys (AKfycb...) are secrets that should not appear in logs.
    /// </summary>
    public static string GetMaskedAppsScriptUrl()
    {
        if (string.IsNullOrEmpty(AppsScriptUrl)) return "";
        try
        {
            var uri = new System.Uri(AppsScriptUrl);
            var path = uri.AbsolutePath;
            var masked = path.Length > 10 ? path[..10] + "..." : path;
            return $"{uri.Scheme}://{uri.Host}{masked}";
        }
        catch
        {
            return "<invalid-url>";
        }
    }
    
    public static void ParseArgs(string[] args)
    {
        foreach (var arg in args)
        {
            // Finding 6: Use Substring instead of Replace to avoid stripping duplicate prefixes
            if (arg.StartsWith("--storeId="))
            {
                if (int.TryParse(arg.Substring("--storeId=".Length), out var storeId))
                    StoreId = storeId;
            }
            else if (arg.StartsWith("--deviceId="))
            {
                // Finding 9: Reject empty values to prevent bypassing login check
                var val = arg.Substring("--deviceId=".Length);
                if (!string.IsNullOrWhiteSpace(val))
                    DeviceId = val;
            }
            else if (arg.StartsWith("--planType="))
            {
                PlanType = arg.Substring("--planType=".Length);
            }
            else if (arg.StartsWith("--apiBaseUrl="))
            {
                ApiBaseUrl = arg.Substring("--apiBaseUrl=".Length);
            }
            else if (arg.StartsWith("--priceLayout6="))
            {
                if (decimal.TryParse(arg.Substring("--priceLayout6=".Length), out var p6))
                    PriceLayout6 = p6;
            }
            else if (arg.StartsWith("--priceLayout2="))
            {
                if (decimal.TryParse(arg.Substring("--priceLayout2=".Length), out var p2))
                    PriceLayout2 = p2;
            }
            else if (arg.StartsWith("--googleDriveEnabled="))
            {
                GoogleDriveEnabled = bool.TryParse(arg.Substring("--googleDriveEnabled=".Length), out var gde) && gde;
            }
            else if (arg.StartsWith("--googleDrivePath="))
            {
                var path = arg.Substring("--googleDrivePath=".Length);
                if (!string.IsNullOrWhiteSpace(path))
                    GoogleDrivePath = path;
            }
            else if (arg.StartsWith("--appsScriptUrl="))
            {
                var url = arg.Substring("--appsScriptUrl=".Length);
                if (!string.IsNullOrWhiteSpace(url))
                    AppsScriptUrl = url;
            }
        }
        
        System.Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, PriceLayout6={PriceLayout6}, PriceLayout2={PriceLayout2}, GoogleDriveEnabled={GoogleDriveEnabled}, GoogleDrivePath={GetMaskedGDrivePath()}, AppsScriptUrl={GetMaskedAppsScriptUrl()}");
    }
}
