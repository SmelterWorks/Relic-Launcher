using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Themes;
using Xunit;

namespace RelicLauncher.Themes.Tests;

public class BuiltInThemeCatalogTests
{
    [Fact]
    public void Catalog_ContainsRelicDefaultAndHighContrast()
    {
        var catalog = new BuiltInThemeCatalog();
        catalog.GetThemes().Should().HaveCount(2);
        catalog.FindById(BuiltInThemeCatalog.RelicDefaultId).Should().NotBeNull();
        catalog.FindById(BuiltInThemeCatalog.HighContrastId).Should().NotBeNull();
    }

    [Theory]
    [InlineData("RELIC-DEFAULT")]
    [InlineData("relic-default")]
    [InlineData("Relic-Default")]
    public void FindById_IsCaseInsensitive(string themeId)
    {
        var catalog = new BuiltInThemeCatalog();
        catalog.FindById(themeId)!.Id.Should().Be(LauncherSettings.DefaultThemeId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindById_ReturnsNull_ForBlankId(string? themeId)
    {
        var catalog = new BuiltInThemeCatalog();
        catalog.FindById(themeId!).Should().BeNull();
    }

    [Fact]
    public void FindById_ReturnsNull_ForUnknownTheme()
    {
        var catalog = new BuiltInThemeCatalog();
        catalog.FindById("does-not-exist").Should().BeNull();
    }

    [Fact]
    public void GetThemes_ReturnsBuiltInResourceUris()
    {
        var catalog = new BuiltInThemeCatalog();
        var themes = catalog.GetThemes();

        themes.Should().OnlyContain(t => t.IsBuiltIn);
        themes.Select(t => t.ResourceUri).Should().Contain(BuiltInThemeCatalog.RelicDefaultUri);
        themes.Select(t => t.ResourceUri).Should().Contain(BuiltInThemeCatalog.HighContrastUri);
    }
}
