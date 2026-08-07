using FluentAssertions;
using RelicLauncher.Infrastructure.Server;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ServerListenEndpointResolverTests
{
    [Fact]
    public void Resolve_IncludesLoopbackAndConfiguredPort()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "serverconfig.json"), """{ "Port": 50030 }""");

        var endpoints = ServerListenEndpointResolver.Resolve(dir.Path);

        endpoints.Should().Contain("127.0.0.1:50030");
        endpoints.Should().Contain("[::1]:50030");
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
