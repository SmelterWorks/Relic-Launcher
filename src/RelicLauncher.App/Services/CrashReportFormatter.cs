using System.Runtime.InteropServices;
using System.Text;
using RelicLauncher.Core;

namespace RelicLauncher.App.Services;

internal static class CrashReportFormatter
{
    public static string Format(Exception exception, bool recovered, string? logsDirectory = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Relic Launcher error report");
        sb.AppendLine($"Version: {BuildMetadata.Version}");
        sb.AppendLine($"Commit: {BuildMetadata.CommitSha}");
        sb.AppendLine($"Built: {BuildMetadata.BuildTimeUtc}");
        sb.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Recovered: {recovered}");
        if (!string.IsNullOrWhiteSpace(logsDirectory))
        {
            sb.AppendLine($"Logs: {logsDirectory}");
        }

        sb.AppendLine();
        sb.AppendLine(exception.ToString());
        return sb.ToString();
    }
}
