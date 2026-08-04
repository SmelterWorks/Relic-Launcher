using FluentAssertions;
using RelicLauncher.App.Services;
using Xunit;

namespace RelicLauncher.App.Tests;

public class CrashReportFormatterTests
{
    [Fact]
    public void Format_IncludesExceptionAndBuildMetadata()
    {
        var report = CrashReportFormatter.Format(new InvalidOperationException("boom"), recovered: true, logsDirectory: "/tmp/logs");

        report.Should().Contain("Relic Launcher error report");
        report.Should().Contain("boom");
        report.Should().Contain("Recovered: True");
        report.Should().Contain("/tmp/logs");
    }
}
