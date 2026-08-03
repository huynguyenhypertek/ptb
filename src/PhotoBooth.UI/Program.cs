using Avalonia;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PhotoBooth.UI.Services;

namespace PhotoBooth.UI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Fix macOS camera auth issue where OpenCV cannot spin main run loop
        Environment.SetEnvironmentVariable("OPENCV_AVFOUNDATION_SKIP_AUTH", "1");
        
        DeviceConfig.ParseArgs(args);

        // Cleanup session folders older than 72 hours to prevent disk exhaustion
        // during long-running kiosk operation. Only deletes the session directories,
        // not the photos inside Google Drive (those sync independently).
        SessionService.CleanupStaleSessions(TimeSpan.FromHours(72));

        // Background memory monitoring — logs every 60s for diagnosing leaks during 12h operation
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await Task.Delay(60_000);
                    var process = Process.GetCurrentProcess();
                    Console.WriteLine(
                        $"[MEM] WorkingSet={process.WorkingSet64 / 1024 / 1024}MB, " +
                        $"GC.Managed={GC.GetTotalMemory(false) / 1024 / 1024}MB, " +
                        $"Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");
                }
                catch { }
            }
        });

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
