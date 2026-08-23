using FluentAssertions;
using Xunit;

namespace RelicLauncher.App.Tests;

public sealed class RelicComboBoxContractTests
{
    [Fact]
    public void RelicComboBox_control_uses_explicit_text_rendering()
    {
        var controlPath = FindRepoFile("src/RelicLauncher.App/Views/Controls/RelicComboBox.axaml");
        var axaml = File.ReadAllText(controlPath);

        axaml.Should().Contain(@"x:Name=""SelectedText""");
        axaml.Should().Contain(@"Foreground=""{DynamicResource Theme.Text}""");
        axaml.Should().Contain(@"x:Name=""ItemsList""");
        axaml.Should().Contain(@"Data=""M 0,0 L 8,0 L 4,5 Z""");
    }

    [Fact]
    public void App_loads_relic_combo_box_styles_after_fluent()
    {
        var axaml = File.ReadAllText(FindRepoFile("src/RelicLauncher.App/App.axaml"));

        var fluent = axaml.IndexOf("<FluentTheme", StringComparison.Ordinal);
        var combo = axaml.IndexOf("Styles/RelicComboBox.axaml", StringComparison.Ordinal);

        fluent.Should().BeGreaterThanOrEqualTo(0);
        combo.Should().BeGreaterThan(fluent);
    }

    [Fact]
    public void Pages_use_relic_combo_box_instead_of_combo_box()
    {
        var pagesRoot = Path.GetDirectoryName(FindRepoFile("src/RelicLauncher.App/Views/Pages/HomePage.axaml"))!;
        var pages = Directory.GetFiles(pagesRoot, "*.axaml", SearchOption.TopDirectoryOnly);

        foreach (var page in pages)
        {
            var axaml = File.ReadAllText(page);
            axaml.Should().NotContain("<ComboBox", $"{page} should not use Avalonia ComboBox");
        }
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate {relativePath} from test output directory.");
    }
}
