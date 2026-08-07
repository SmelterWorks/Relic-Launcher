using FluentAssertions;
using RelicLauncher.Core.Server;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class VintageStoryServerConfigReaderTests
{
    [Fact]
    public void TryReadPort_ReturnsDefaultWhenMissingFile()
    {
        VintageStoryServerConfigReader.TryReadPort(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
            .Should().BeNull();
    }

    [Fact]
    public void TryReadPort_ReadsPortProperty()
    {
        using var dir = new TempDir();
        var json = """{ "Port": 42421 }""";
        File.WriteAllText(Path.Combine(dir.Path, "serverconfig.json"), json);

        VintageStoryServerConfigReader.TryReadPort(dir.Path).Should().Be(42421);
    }

    [Fact]
    public void TryReadPort_ReadsStringPortProperty()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "serverconfig.json"), """{ "port": "50030" }""");

        VintageStoryServerConfigReader.TryReadPort(dir.Path).Should().Be(50030);
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
