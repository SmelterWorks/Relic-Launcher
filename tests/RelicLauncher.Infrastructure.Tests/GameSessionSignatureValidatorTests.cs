using FluentAssertions;
using RelicLauncher.Infrastructure.Auth;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class GameSessionSignatureValidatorTests
{
    [Fact]
    public void IsValid_ReturnsFalse_WhenArgumentsMissing()
    {
        var validator = new GameSessionSignatureValidator();

        validator.IsValid(null, "sig", "uid").Should().BeFalse();
        validator.IsValid("key", null, "uid").Should().BeFalse();
        validator.IsValid("key", "sig", null).Should().BeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenSignatureIsInvalid()
    {
        var validator = new GameSessionSignatureValidator();

        validator.IsValid("session-key", "not-base64-signature!!!", "player-uid").Should().BeFalse();
    }
}
