using FluentAssertions;
using Xunit;

namespace RelicLauncher.App.Tests;

public sealed class ComboBoxStyleContractTests
{
    [Fact]
    public void ComboBox_template_does_not_use_fluent_star_column()
    {
        var path = FindRepoFile("src/RelicLauncher.App/Styles/ComboBox.axaml");
        var axaml = File.ReadAllText(path);

        axaml.Should().NotContain(@"ColumnDefinitions=""*,32""");
        axaml.Should().Contain(@"<RowDefinition Height=""0"" />");
        axaml.Should().Contain(@"MaxHeight=""36""");
        axaml.Should().Contain(@"Data=""M 0,0 L 8,0 L 4,5 Z""");
        axaml.Should().Contain(@"Content=""{Binding SelectedItem, RelativeSource={RelativeSource TemplatedParent}}""");
        axaml.Should().Contain(@"Name=""PART_Popup""");
        axaml.Should().Contain(@"Name=""PART_ItemsPresenter""");
    }

    [Fact]
    public void App_loads_combo_box_styles_after_fluent()
    {
        var axaml = File.ReadAllText(FindRepoFile("src/RelicLauncher.App/App.axaml"));

        var fluent = axaml.IndexOf("<FluentTheme", StringComparison.Ordinal);
        var combo = axaml.IndexOf("Styles/ComboBox.axaml", StringComparison.Ordinal);

        fluent.Should().BeGreaterThanOrEqualTo(0);
        combo.Should().BeGreaterThan(fluent);
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
