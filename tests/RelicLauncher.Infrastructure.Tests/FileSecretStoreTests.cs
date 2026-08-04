using FluentAssertions;
using RelicLauncher.Infrastructure.Security;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class FileSecretStoreTests
{
    [Fact]
    public async Task SetGetDelete_RoundTripsValue()
    {
        using var temp = new TempAppPaths();
        var store = new FileSecretStore(new FixedPathProvider(temp.Paths));

        (await store.SetAsync("k", "value")).IsSuccess.Should().BeTrue();
        (await store.GetAsync("k")).Value.Should().Be("value");
        (await store.DeleteAsync("k")).IsSuccess.Should().BeTrue();
        (await store.GetAsync("k")).Value.Should().BeNull();
    }
}
