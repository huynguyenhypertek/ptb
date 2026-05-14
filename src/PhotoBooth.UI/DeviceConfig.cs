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
        }
        
        System.Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}, ApiBaseUrl={ApiBaseUrl}, PriceLayout6={PriceLayout6}, PriceLayout2={PriceLayout2}");
    }
}
