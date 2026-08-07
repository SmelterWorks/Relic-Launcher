using FluentAssertions;
using RelicLauncher.Core.Security;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class ConnectAddressValidatorTests
{
    [Theory]
    [InlineData("tops.vintagestory.at", "tops.vintagestory.at:42420")]
    [InlineData("192.168.0.10:50030", "192.168.0.10:50030")]
    [InlineData("vintagestoryjoin://tops.vintagestory.at", "tops.vintagestory.at:42420")]
    [InlineData("localhost", "localhost:42420")]
    public void TryNormalize_AcceptsValidAddresses(string input, string expected)
    {
        ConnectAddressValidator.TryNormalize(input, out var normalized, out var error).Should().BeTrue();
        normalized.Should().Be(expected);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("host/with/slash")]
    public void TryNormalize_RejectsUnsafeInput(string input)
    {
        ConnectAddressValidator.TryNormalize(input, out _, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }
}
