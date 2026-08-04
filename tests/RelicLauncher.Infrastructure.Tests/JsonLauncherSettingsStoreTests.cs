using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Settings;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class JsonLauncherSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsSettings()
    {
        using var temp = new TempAppPaths();
        var store = CreateStore(temp);

        var settings = new LauncherSettings
        {
            SelectedThemeId = "high-contrast",
            GameInstallPath = "/opt/vintagestory",
            ConfirmBeforeExit = true,
            HomeBackgroundLogoMode = HomeBackgroundLogoMode.Banner,
            HomeBackgroundCustomLogoPath = "/tmp/logo.png",
            HomeBackgroundLogoOpacity = 0.2,
        };

        var save = await store.SaveAsync(settings);
        save.IsSuccess.Should().BeTrue(save.Error);

        var load = await store.LoadAsync();
        load.IsSuccess.Should().BeTrue(load.Error);
        load.Value!.SelectedThemeId.Should().Be("high-contrast");
        load.Value.GameInstallPath.Should().Be("/opt/vintagestory");
        load.Value.ConfirmBeforeExit.Should().BeTrue();
        load.Value.HomeBackgroundLogoMode.Should().Be(HomeBackgroundLogoMode.Banner);
        load.Value.HomeBackgroundCustomLogoPath.Should().Be("/tmp/logo.png");
        load.Value.HomeBackgroundLogoOpacity.Should().Be(0.2);
    }

    [Fact]
    public async Task LoadAsync_CreatesDefaults_WhenSettingsFileMissing()
    {
        using var temp = new TempAppPaths();
        var store = CreateStore(temp);

        var load = await store.LoadAsync();

        load.IsSuccess.Should().BeTrue();
        load.Value!.SelectedThemeId.Should().Be(LauncherSettings.DefaultThemeId);
        File.Exists(temp.Paths.SettingsFile).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultTheme_WhenThemeIdBlank()
    {
        using var temp = new TempAppPaths();
        Directory.CreateDirectory(temp.Paths.RootDirectory);
        await File.WriteAllTextAsync(temp.Paths.SettingsFile, """{"selectedThemeId":"  "}""");
        var store = CreateStore(temp);

        var load = await store.LoadAsync();

        load.IsSuccess.Should().BeTrue();
        load.Value!.SelectedThemeId.Should().Be(LauncherSettings.DefaultThemeId);
    }

    [Fact]
    public async Task LoadAsync_Fails_WhenJsonInvalid()
    {
        using var temp = new TempAppPaths();
        Directory.CreateDirectory(temp.Paths.RootDirectory);
        await File.WriteAllTextAsync(temp.Paths.SettingsFile, "{ not json");
        var store = CreateStore(temp);

        var load = await store.LoadAsync();

        load.IsSuccess.Should().BeFalse();
        load.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenSettingsNull()
    {
        using var temp = new TempAppPaths();
        var store = CreateStore(temp);

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!));
    }

    private static JsonLauncherSettingsStore CreateStore(TempAppPaths temp)
        => new(new FixedPathProvider(temp.Paths), NullLogger<JsonLauncherSettingsStore>.Instance);
}
