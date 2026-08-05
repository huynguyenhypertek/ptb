using System.IO;
using Xunit;
using PhotoBooth.Admin.ViewModels;
using PhotoBooth.Admin.Services;

namespace PhotoBooth.Admin.Tests.ViewModels;

public class DeviceLauncherViewModelTests
{
    [Fact]
    public void IsEventMode_DefaultIsFalse()
    {
        // Arrange
        var apiService = new ApiService();
        var settingsService = new SettingsService();
        
        // Act
        var viewModel = new DeviceLauncherViewModel(apiService, settingsService);
        
        // Assert
        Assert.False(viewModel.IsEventMode, "IsEventMode should be false by default (UI Mode).");
    }

    [Fact]
    public void IsEventMode_CanBeToggledToTrue()
    {
        // Arrange
        var apiService = new ApiService();
        var settingsService = new SettingsService();
        var viewModel = new DeviceLauncherViewModel(apiService, settingsService);
        
        // Act
        viewModel.IsEventMode = true;
        
        // Assert
        Assert.True(viewModel.IsEventMode, "IsEventMode should be settable to true (Event Mode).");
    }

    [Fact]
    public void CreateLaunchStartInfo_UiMode_ResolvesToUiBinaryOrProject()
    {
        // Arrange
        var apiService = new ApiService();
        var settingsService = new SettingsService();
        var viewModel = new DeviceLauncherViewModel(apiService, settingsService)
        {
            IsEventMode = false
        };

        var launchItem = new DeviceLaunchItem
        {
            User = new UserItem { Username = "TestDevice1" }
        };

        // Act
        var startInfo = viewModel.CreateLaunchStartInfo(launchItem);

        // Assert
        Assert.NotNull(startInfo);
        
        // Check if it's returning the packaged binary or dev mode
        if (startInfo.FileName.EndsWith("PhotoBooth.UI"))
        {
            // Packaged mode
            Assert.Contains("PhotoBooth.UI", startInfo.FileName);
        }
        else
        {
            // Dev mode
            Assert.Contains("dotnet", startInfo.FileName.ToLower());
            Assert.Contains("PhotoBooth.UI", startInfo.Arguments);
        }
    }

    [Fact]
    public void CreateLaunchStartInfo_EventMode_ResolvesToEventBinaryOrProject()
    {
        // Arrange
        var apiService = new ApiService();
        var settingsService = new SettingsService();
        var viewModel = new DeviceLauncherViewModel(apiService, settingsService)
        {
            IsEventMode = true
        };

        var launchItem = new DeviceLaunchItem
        {
            User = new UserItem { Username = "TestDevice2" }
        };

        // Act
        var startInfo = viewModel.CreateLaunchStartInfo(launchItem);

        // Assert
        Assert.NotNull(startInfo);
        
        // Check if it's returning the packaged binary or dev mode
        if (startInfo.FileName.EndsWith("PhotoBooth.Event"))
        {
            // Packaged mode
            Assert.Contains("PhotoBooth.Event", startInfo.FileName);
        }
        else
        {
            // Dev mode
            Assert.Contains("dotnet", startInfo.FileName.ToLower());
            Assert.Contains("PhotoBooth.Event", startInfo.Arguments);
        }
    }
}
