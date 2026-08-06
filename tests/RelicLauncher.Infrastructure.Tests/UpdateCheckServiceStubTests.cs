using FluentAssertions;
using RelicLauncher.Infrastructure.Stubs;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class UpdateCheckServiceStubTests
{
    [Fact]
    public async Task CheckForLauncherUpdateAsync_ReturnsNull()
    {
        var service = new UpdateCheckServiceStub();
        var result = await service.CheckForLauncherUpdateAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
