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
        public bool PrintCalled => PrintCallCount > 0;
        public int PrintCallCount { get; private set; }
        public string LastImagePath { get; private set; } = "";
        public int LastCopies { get; private set; }
        public int Delay { get; set; } = 0;

        public async Task<bool> PrintImageAsync(string imagePath, string printerName, int copies, CancellationToken ct)
        {
            PrintCallCount++;
            LastImagePath = imagePath;
            LastCopies = copies;
            if (Delay > 0)
            {
                await Task.Delay(Delay, ct);
            }
            return true;
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
    public async Task StartPrintingCommand_ConcurrentExecutions_OnlyPrintsOnce()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();
        printService.Delay = 200; // Simulate long print

        sessionService.CurrentSession.FinalImagePath = "dummy_path.jpg";

        using var vm = new ReviewPrintViewModel(navService, sessionService, printService);
        
        var t1 = vm.StartPrintingCommand.ExecuteAsync(null);
        var t2 = vm.StartPrintingCommand.ExecuteAsync(null);
        
        await Task.WhenAll(t1, t2);

        Assert.Equal(1, printService.PrintCallCount);
    }

    [Fact]
    public async Task GenerateQROverlayAsync_Completes_TriggersAutomaticPrint()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();

        // Setup session path
        sessionService.CurrentSession.FinalImagePath = "dummy_path.jpg";
        sessionService.CurrentSession.PreFetchedDriveUrl = "http://dummy.url"; // use pre-fetched URL to make QR generation fast

        using var vm = new ReviewPrintViewModel(navService, sessionService, printService);
        
        // Wait for background QR generation and subsequent print to finish
        for (int i = 0; i < 50; i++)
        {
            if (printService.PrintCallCount > 0) break;
            await Task.Delay(100);
        }

        Assert.Equal(1, printService.PrintCallCount);
    }

    [Fact]
    public void ReturnToStartCommand_ResetsSessionAndNavigates()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();

        // Register dummy factory to observe navigation
        bool navigated = false;
        navService.RegisterViewModel<StartViewModel>(() =>
        {
            navigated = true;
            return null!;
        });

        var oldSession = sessionService.CurrentSession;

        using var vm = new ReviewPrintViewModel(navService, sessionService, printService);
        vm.ReturnToStartCommand.Execute(null);

        Assert.NotSame(oldSession, sessionService.CurrentSession);
        Assert.True(navigated);
    }

    [Fact]
    public async Task IdleTimer_NavigatesToStartAfterTimeout()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();

        bool navigated = false;
        navService.RegisterViewModel<StartViewModel>(() =>
        {
            navigated = true;
            return null!;
        });

        using var vm = new ReviewPrintViewModel(navService, sessionService, printService);
        
        vm.IdleTimeoutMs = 100;
        vm.IncreaseCopiesCommand.Execute(null); // Triggers StartIdleTimer() with 100ms

        await Task.Delay(500); // Wait enough time for Avalonia Dispatcher to process

        Assert.True(navigated);
    }

    [Fact]
    public async Task ReturnToStartCommand_CancelsIdleTimer()
    {
        var navService = new NavigationService();
        var sessionService = new SessionService();
        var printService = new MockPrintService();

        bool navigated = false;
        int navCount = 0;
        navService.RegisterViewModel<StartViewModel>(() =>
        {
            navigated = true;
            navCount++;
            return null!;
        });

        using var vm = new ReviewPrintViewModel(navService, sessionService, printService);
        vm.IdleTimeoutMs = 200; 
        vm.IncreaseCopiesCommand.Execute(null); // Restart timer with 200ms

        // Manually trigger early
        vm.ReturnToStartCommand.Execute(null);

        // Reset tracking
        navCount = 0;

        // Wait for timer that should have been cancelled
        await Task.Delay(500); // 500ms is well past the 200ms timer

        // It should NOT have navigated again
        Assert.Equal(0, navCount);
    }
}
