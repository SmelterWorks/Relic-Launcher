using FluentAssertions;
using RelicLauncher.Core.Versions;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class GameVersionComparerTests
{
    [Theory]
    [InlineData("1.22.6", "1.22.5", 1)]
    [InlineData("1.21.0", "1.22.0", -1)]
    [InlineData("1.20.0", "1.20.0", 0)]
    public void Compare_OrdersSemVerLikeStrings(string left, string right, int expectedSign)
    {
        var cmp = GameVersionComparer.Compare(left, right);
        Math.Sign(cmp).Should().Be(expectedSign);
    }
}
