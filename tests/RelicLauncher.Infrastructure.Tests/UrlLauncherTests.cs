using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.Paths;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class UrlLauncherTests
{
    private readonly UrlLauncher _launcher = new(NullLogger<UrlLauncher>.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenUrl_Fails_WhenUrlBlank(string? url)
    {
        var result = _launcher.OpenUrl(url!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }
}
