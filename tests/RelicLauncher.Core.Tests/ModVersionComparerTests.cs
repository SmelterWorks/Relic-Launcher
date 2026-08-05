using FluentAssertions;
using RelicLauncher.Core.Mods;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class ModVersionComparerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    public void Satisfies_AnyMinimum_IsTrue(string? minimum)
    {
        ModVersionComparer.Satisfies("1.0.0", minimum).Should().BeTrue();
        ModVersionComparer.Satisfies(null, minimum).Should().BeTrue();
    }

    [Fact]
    public void Satisfies_InstalledMeetsMinimum()
    {
        ModVersionComparer.Satisfies("2.0.0", "1.5.0").Should().BeTrue();
        ModVersionComparer.Satisfies("1.5.0", "1.5.0").Should().BeTrue();
        ModVersionComparer.Satisfies("1.4.0", "1.5.0").Should().BeFalse();
        ModVersionComparer.Satisfies(null, "1.0.0").Should().BeFalse();
    }

    [Fact]
    public void TakeHigherMinimum_PicksStrictest()
    {
        ModVersionComparer.TakeHigherMinimum("1.0.0", "2.0.0").Should().Be("2.0.0");
        ModVersionComparer.TakeHigherMinimum("*", "1.2.3").Should().Be("1.2.3");
        ModVersionComparer.TakeHigherMinimum("1.2.3", "*").Should().Be("1.2.3");
        ModVersionComparer.TakeHigherMinimum("*", "").Should().BeNull();
    }
}
