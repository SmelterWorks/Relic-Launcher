using System.Text;
using FluentAssertions;
using RelicLauncher.Infrastructure.Security;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class PlatformSecretStoreTests
{
    [Fact]
    public async Task SetGetDelete_RoundTripsValue()
    {
        using var temp = new TempAppPaths();
        var store = new PlatformSecretStore(new FixedPathProvider(temp.Paths));

        (await store.SetAsync("k", "value")).IsSuccess.Should().BeTrue();
        (await store.GetAsync("k")).Value.Should().Be("value");
        (await store.DeleteAsync("k")).IsSuccess.Should().BeTrue();
        (await store.GetAsync("k")).Value.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Succeeds_WhenKeyMissing()
    {
        using var temp = new TempAppPaths();
        var store = new PlatformSecretStore(new FixedPathProvider(temp.Paths));

        var result = await store.DeleteAsync("missing");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForMissingKey()
    {
        using var temp = new TempAppPaths();
        var store = new PlatformSecretStore(new FixedPathProvider(temp.Paths));

        var result = await store.GetAsync("missing");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Get_MigratesLegacyV1Payload()
    {
        using var temp = new TempAppPaths();
        Directory.CreateDirectory(temp.Paths.SecretsDirectory);
        const string key = "legacy";
        var safe = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32].ToLowerInvariant();
        var path = Path.Combine(temp.Paths.SecretsDirectory, safe + ".bin");
        var legacy = LegacyV1Crypto.Encrypt(Encoding.UTF8.GetBytes("migrated-secret"));
        await File.WriteAllBytesAsync(path, legacy);

        var store = new PlatformSecretStore(new FixedPathProvider(temp.Paths));
        var result = await store.GetAsync(key);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("migrated-secret");

        var rewritten = await File.ReadAllBytesAsync(path);
        rewritten.AsSpan(0, 4).SequenceEqual("RLS2"u8).Should().BeTrue();
    }
}
