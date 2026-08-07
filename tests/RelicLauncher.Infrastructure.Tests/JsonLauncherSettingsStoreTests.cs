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
            ModUpdateMode = ModUpdateMode.Automatic,
            ModUpdateOptOutModIds = ["sample", "other"],
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
        load.Value.ModUpdateMode.Should().Be(ModUpdateMode.Automatic);
        load.Value.ModUpdateOptOutModIds.Should().BeEquivalentTo(["sample", "other"]);
        load.Value.Endpoints.AccountBaseUrl.Should().Be("https://account.vintagestory.at/");
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsEndpointOverrides()
    {
        using var temp = new TempAppPaths();
        var store = CreateStore(temp);

        var settings = new LauncherSettings
        {
            Endpoints = new EndpointSettings
            {
                AccountBaseUrl = "https://account.example/",
                ModDbApiBaseUrl = "https://mods.example/api/",
                VersionCatalogUrl = "https://api.example/versions.json",
                NewsBlogUrl = "https://news.example/blog/",
                WikiBaseUrl = "https://wiki.example/",
            },
        };

        var save = await store.SaveAsync(settings);
        save.IsSuccess.Should().BeTrue(save.Error);

        var load = await store.LoadAsync();
        load.IsSuccess.Should().BeTrue(load.Error);
        load.Value!.Endpoints.AccountBaseUrl.Should().Be("https://account.example/");
        load.Value.Endpoints.ModDbApiBaseUrl.Should().Be("https://mods.example/api/");
        load.Value.Endpoints.VersionCatalogUrl.Should().Be("https://api.example/versions.json");
        load.Value.Endpoints.NewsBlogUrl.Should().Be("https://news.example/blog/");
        load.Value.Endpoints.WikiBaseUrl.Should().Be("https://wiki.example/");
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
    public async Task LoadAsync_HydratesNullEndpoints()
    {
        using var temp = new TempAppPaths();
        Directory.CreateDirectory(temp.Paths.RootDirectory);
        await File.WriteAllTextAsync(temp.Paths.SettingsFile, """{"selectedThemeId":"relic-default"}""");
        var store = CreateStore(temp);

        var load = await store.LoadAsync();

        load.IsSuccess.Should().BeTrue();
        load.Value!.Endpoints.Should().NotBeNull();
        load.Value.Endpoints.AccountBaseUrl.Should().NotBeNullOrWhiteSpace();
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
