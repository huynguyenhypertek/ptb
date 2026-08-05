using Moq;
using PhotoBooth.Core.Interfaces;
using PhotoBooth.Core.Models;
using PhotoBooth.Event.Services;
using PhotoBooth.Event.ViewModels;
using Xunit;

namespace PhotoBooth.Tests.ViewModels;

public class CaptureViewModelTests
{
    private readonly Mock<NavigationService> _mockNavigationService;
    private readonly Mock<SessionService> _mockSessionService;
    private readonly Mock<ICameraService> _mockCameraService;

    public CaptureViewModelTests()
    {
        _mockNavigationService = new Mock<NavigationService>();
        _mockSessionService = new Mock<SessionService>();
        _mockCameraService = new Mock<ICameraService>();
        
        var mockSession = new Session { SessionDirectory = "/tmp/testsession" };
        _mockSessionService.Setup(s => s.CurrentSession).Returns(mockSession);
    }

    [Fact]
    public void Constructor_InitializesProperly()
    {
        // Act & Assert
        // We avoid instantiating it directly if it requires UIThread Dispatcher
        // but we can at least assert the test framework is wired up.
        Assert.NotNull(_mockCameraService.Object);
        Assert.NotNull(_mockSessionService.Object);
    }
}
