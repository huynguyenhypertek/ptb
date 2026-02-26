namespace PhotoBooth.UI;

/// <summary>
/// Stores device info passed from Admin app login (via command-line args)
/// </summary>
public static class DeviceConfig
{
    public static int? StoreId { get; set; }
    public static string DeviceId { get; set; } = "device-1";
    public static string PlanType { get; set; } = "Pro";  // "Basic" or "Pro"
    public static string ApiBaseUrl { get; set; } = "https://intellective-unimpinging-greyson.ngrok-free.dev";
    
    public static void ParseArgs(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--storeId="))
            {
                if (int.TryParse(arg.Replace("--storeId=", ""), out var storeId))
                    StoreId = storeId;
            }
            else if (arg.StartsWith("--deviceId="))
            {
                DeviceId = arg.Replace("--deviceId=", "");
            }
            else if (arg.StartsWith("--planType="))
            {
                PlanType = arg.Replace("--planType=", "");
            }
        }
        
        System.Console.WriteLine($"[CONFIG] StoreId={StoreId}, DeviceId={DeviceId}, PlanType={PlanType}");
    }
}
