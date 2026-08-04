using FluentAssertions;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Models;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.App.Tests;

public class HomeBackgroundLogoResolverTests
{
    [Fact]
    public void Resolve_None_HidesLogo()
    {
        var settings = new LauncherSettings { HomeBackgroundLogoMode = HomeBackgroundLogoMode.None };

        var state = HomeBackgroundLogoResolver.Resolve(settings);

        state.ShowLogo.Should().BeFalse();
        state.Source.Should().BeNull();
    }

    [Theory]
    [InlineData(HomeBackgroundLogoMode.Square, HomeBackgroundLogoResolver.SquareLogoUri)]
    [InlineData(HomeBackgroundLogoMode.Banner, HomeBackgroundLogoResolver.BannerLogoUri)]
    public void Resolve_BuiltInLogo_ReturnsBundledUri(HomeBackgroundLogoMode mode, string expectedUri)
    {
        var settings = new LauncherSettings { HomeBackgroundLogoMode = mode };

        var state = HomeBackgroundLogoResolver.Resolve(settings);

        state.ShowLogo.Should().BeTrue();
        state.Source.Should().Be(expectedUri);
    }

    [Fact]
    public void Resolve_Custom_UsesExistingFilePath()
    {
        using var temp = new TempAppPaths();
        var logoPath = Path.Combine(temp.Paths.RootDirectory, "logo.png");
        File.WriteAllText(logoPath, "png");
        var settings = new LauncherSettings
        {
            HomeBackgroundLogoMode = HomeBackgroundLogoMode.Custom,
            HomeBackgroundCustomLogoPath = logoPath,
        };

        var state = HomeBackgroundLogoResolver.Resolve(settings);

        state.ShowLogo.Should().BeTrue();
        state.Source.Should().Be(logoPath);
    }

    [Fact]
    public void Resolve_Custom_HidesLogo_WhenFileMissing()
    {
        var settings = new LauncherSettings
        {
            HomeBackgroundLogoMode = HomeBackgroundLogoMode.Custom,
            HomeBackgroundCustomLogoPath = "/tmp/does-not-exist.png",
        };

        var state = HomeBackgroundLogoResolver.Resolve(settings);

        state.ShowLogo.Should().BeFalse();
        state.Source.Should().BeNull();
    }

    [Theory]
    [InlineData(-1.0, 0.02)]
    [InlineData(0.0, 0.02)]
    [InlineData(2.0, 1.0)]
    public void Resolve_ClampsOpacity(double input, double expected)
    {
        var settings = new LauncherSettings
        {
            HomeBackgroundLogoMode = HomeBackgroundLogoMode.Square,
            HomeBackgroundLogoOpacity = input,
        };

        var state = HomeBackgroundLogoResolver.Resolve(settings);

        state.Opacity.Should().Be(expected);
    }
}
