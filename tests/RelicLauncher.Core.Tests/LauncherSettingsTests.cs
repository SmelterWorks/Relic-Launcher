using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class LauncherSettingsTests
{
    [Fact]
    public void Defaults_UseRelicDefaultThemeAndSquareLogo()
    {
        var settings = new LauncherSettings();

        settings.SelectedThemeId.Should().Be(LauncherSettings.DefaultThemeId);
        settings.HomeBackgroundLogoMode.Should().Be(HomeBackgroundLogoMode.Square);
        settings.HomeBackgroundLogoOpacity.Should().Be(0.2);
        settings.ConfirmBeforeExit.Should().BeFalse();
        settings.GameInstallPath.Should().BeNull();
        settings.InstallsRoot.Should().BeNull();
        settings.SelectedVersion.Should().BeNull();
        settings.DataPath.Should().BeNull();
    }
}
