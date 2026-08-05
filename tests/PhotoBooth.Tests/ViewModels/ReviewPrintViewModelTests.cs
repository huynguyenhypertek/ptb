using System.Threading;
using System.Threading.Tasks;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Event.Services;
using PhotoBooth.Event.ViewModels;
using Xunit;

namespace PhotoBooth.Tests.ViewModels;

public class ReviewPrintViewModelTests
{
    private class MockPrintService : IPrintService
    {
        public bool PrintCalled { get; private set; }
        public string LastImagePath { get; private set; } = "";
        public int LastCopies { get; private set; }

        public Task<bool> PrintImageAsync(string imagePath, string printerName, int copies, CancellationToken ct)
        {
            PrintCalled = true;
            LastImagePath = imagePath;
            LastCopies = copies;
            return Task.FromResult(true);
        }
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();

        using var vm = new ReviewPrintViewModel(navService, sessionService, printService);

        Assert.Equal(1, vm.PrintCopies);
        Assert.False(vm.IsOffline);
        Assert.NotNull(vm.SequentialNumber);
        Assert.NotNull(vm.StartPrintingCommand);
    }

    [Fact]
    public void PrintCopies_IncreaseAndDecrease_BoundsAreRespected()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();

        using var vm = new ReviewPrintViewModel(navService, sessionService, printService);

        // Test Increase
        for (int i = 0; i < 15; i++)
        {
            vm.IncreaseCopiesCommand.Execute(null);
        }
        Assert.Equal(10, vm.PrintCopies); // Max is 10

        // Test Decrease
        for (int i = 0; i < 15; i++)
        {
            vm.DecreaseCopiesCommand.Execute(null);
        }
        Assert.Equal(1, vm.PrintCopies); // Min is 1
    }

    [Fact]
    public async Task StartPrintingCommand_CallsPrintService()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();

        // Setup session path
        sessionService.CurrentSession.FinalImagePath = "dummy_path.jpg";

        using var vm = new ReviewPrintViewModel(navService, sessionService, printService);
        vm.PrintCopies = 3;

        await vm.StartPrintingCommand.ExecuteAsync(null);

        Assert.True(printService.PrintCalled);
        Assert.Equal("dummy_path.jpg", printService.LastImagePath);
        Assert.Equal(3, printService.LastCopies);
        Assert.Equal("Đã gửi lệnh in", vm.StatusText);
        Assert.Equal(100, vm.Progress);
    }

    [Fact]
    public async Task LoadFinalImageAsync_LoadsBitmapFromFile()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();

        var tempPath = Path.GetTempFileName() + ".png";
        
        try
        {
            // Create a valid dummy PNG
            using (var img = new OpenCvSharp.Mat(10, 10, OpenCvSharp.MatType.CV_8UC3, new OpenCvSharp.Scalar(0,0,0)))
            {
                OpenCvSharp.Cv2.ImWrite(tempPath, img);
            }
            
            sessionService.CurrentSession.FinalImagePath = tempPath;

            using var vm = new ReviewPrintViewModel(navService, sessionService, printService);
            
            // Wait for constructor tasks to potentially yield (LoadFinalImageAsync is fired-and-forget)
            await Task.Delay(200);

            Assert.NotNull(vm.FinalImage);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
