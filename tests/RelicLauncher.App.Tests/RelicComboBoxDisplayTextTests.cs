using FluentAssertions;
using RelicLauncher.App.ViewModels;
using RelicLauncher.App.Views.Controls;
using RelicLauncher.Core.Models;
using Xunit;

namespace RelicLauncher.App.Tests;

public sealed class RelicComboBoxDisplayTextTests
{
    [Fact]
    public void GetItemDisplayText_returns_string_value()
    {
        RelicComboBox.GetItemDisplayText("1.21.0").Should().Be("1.21.0");
    }

    [Fact]
    public void GetItemDisplayText_reads_label_and_display_name_properties()
    {
        RelicComboBox.GetItemDisplayText(new ModSortOption { Id = "name", Label = "Name" })
            .Should().Be("Name");

        RelicComboBox.GetItemDisplayText(new ThemeDefinition
        {
            Id = "relic-default",
            DisplayName = "Relic Default",
            IsBuiltIn = true,
        }).Should().Be("Relic Default");
    }
}
