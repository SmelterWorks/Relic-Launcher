using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Infrastructure.Process;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class SafeProcessRunnerTests
{
    private readonly SafeProcessRunner _runner = new(NullLogger<SafeProcessRunner>.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartAsync_Fails_WhenExecutablePathBlank(string? path)
    {
        var result = await _runner.StartAsync(path!, Array.Empty<string>());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task StartAsync_Fails_WhenExecutableMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Vintagestory");

        var result = await _runner.StartAsync(missing, Array.Empty<string>());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task StartAsync_Fails_WhenArgumentIsNull()
    {
        using var script = new TempExecutable();
        var result = await _runner.StartAsync(script.Path, new[] { "ok", null! });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task StartAsync_Fails_WhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _runner.StartAsync("ignored", Array.Empty<string>(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StartAsync_Succeeds_ForExistingExecutable()
    {
        using var script = new TempExecutable();
        var result = await _runner.StartAsync(script.Path, Array.Empty<string>());

        result.IsSuccess.Should().BeTrue();
    }

    private sealed class TempExecutable : IDisposable
    {
        public string Path { get; }
        public string DirectoryPath { get; }

        public TempExecutable()
        {
            DirectoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            Path = System.IO.Path.Combine(DirectoryPath, OperatingSystem.IsWindows() ? "runner.cmd" : "runner.sh");
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllText(Path, "@echo off\r\nexit /b 0\r\n");
            }
            else
            {
                File.WriteAllText(Path, "#!/bin/sh\nexit 0\n");
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    ArgumentList = { "+x", Path },
                    UseShellExecute = false,
                })?.WaitForExit();
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
