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

    [Fact]
    public async Task StartAsync_AppliesEnvironmentVariables()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var script = new TempExecutable(writeDotNetRoot: true);
        var outFile = Path.Combine(script.DirectoryPath, "out.txt");
        var result = await _runner.StartAsync(
            script.Path,
            [outFile],
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["DOTNET_ROOT"] = "/managed/dotnet-root" });

        result.IsSuccess.Should().BeTrue();
        await WaitForFileAsync(outFile).ConfigureAwait(true);
        (await File.ReadAllTextAsync(outFile).ConfigureAwait(true)).Trim().Should().Be("/managed/dotnet-root");
    }

    private static async Task WaitForFileAsync(string path)
    {
        for (var i = 0; i < 50; i++)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }
    }

    private sealed class TempExecutable : IDisposable
    {
        public string Path { get; }
        public string DirectoryPath { get; }

        public TempExecutable(bool writeDotNetRoot = false)
        {
            DirectoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RelicLauncherTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            Path = System.IO.Path.Combine(DirectoryPath, OperatingSystem.IsWindows() ? "runner.cmd" : "runner.sh");
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllText(Path, "@echo off\r\nexit /b 0\r\n");
            }
            else if (writeDotNetRoot)
            {
                File.WriteAllText(Path, "#!/bin/sh\nprintf '%s' \"$DOTNET_ROOT\" > \"$1\"\n");
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    ArgumentList = { "+x", Path },
                    UseShellExecute = false,
                })?.WaitForExit();
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
