using Avalonia;
using System;
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

        // Disabled: User wants to keep photos on disk
        // SessionService.CleanupStaleSessions(TimeSpan.FromHours(24));

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
