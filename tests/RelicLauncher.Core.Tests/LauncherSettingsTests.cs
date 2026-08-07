using FluentAssertions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
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
        settings.HomeBackgroundLogoOpacity.Should().Be(RelicDefaults.HomeBackgroundLogoOpacity);
        settings.ConfirmBeforeExit.Should().BeFalse();
        settings.ModUpdateMode.Should().Be(ModUpdateMode.Prompt);
        settings.LauncherUpdateMode.Should().Be(LauncherUpdateMode.Prompt);
        settings.LauncherUpdateChannel.Should().Be(LauncherUpdateChannel.Stable);
        settings.ModUpdateOptOutModIds.Should().BeEmpty();
        settings.GameInstallPath.Should().BeNull();
        settings.InstallsRoot.Should().BeNull();
        settings.SelectedVersion.Should().BeNull();
        settings.DataPath.Should().BeNull();
        settings.Endpoints.AccountBaseUrl.Should().Be(VintageStoryEndpoints.AccountBaseUrl);
        settings.Endpoints.ModDbApiBaseUrl.Should().Be(VintageStoryEndpoints.ModDbApiBaseUrl);
        settings.Endpoints.VersionCatalogUrl.Should().Be(VintageStoryEndpoints.VersionCatalogUrl);
        settings.Endpoints.NewsBlogUrl.Should().Be(VintageStoryEndpoints.NewsBlogUrl);
        settings.Endpoints.WikiBaseUrl.Should().Be(VintageStoryEndpoints.WikiBaseUrl);
    }
}
