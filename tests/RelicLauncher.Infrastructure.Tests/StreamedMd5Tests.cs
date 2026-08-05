using System.Security.Cryptography;
using FluentAssertions;
using RelicLauncher.Infrastructure.Versions;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class StreamedMd5Tests
{
    [Fact]
    public async Task ComputeMd5Async_MatchesHashData_ForLargeFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"relic-md5-{Guid.NewGuid():N}.bin");
        try
        {
            var bytes = new byte[1024 * 256];
            Random.Shared.NextBytes(bytes);
            await File.WriteAllBytesAsync(path, bytes);

            var expected = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
            var actual = await GameVersionInstaller.ComputeMd5Async(path, CancellationToken.None);

            actual.Should().Be(expected);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
