using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Stubs;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class UpdateCheckServiceStubTests
{
    [Fact]
    public async Task CheckForLauncherUpdateAsync_ReturnsNoUpdate()
    {
        var service = new UpdateCheckServiceStub();
        var result = await service.CheckForLauncherUpdateAsync(new LauncherUpdateCheckRequest
        {
            Channel = LauncherUpdateChannel.Stable,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Update.Should().BeNull();
    }
}
