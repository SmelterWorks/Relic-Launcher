using RelicLauncher.Core.SelfCheck;
using RelicLauncher.Infrastructure.SelfCheck;

namespace RelicLauncher.App.SelfCheck;

internal static class SelfCheckHost
{
    public static bool TryHandle(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (!args.Contains("--self-check", StringComparer.Ordinal))
        {
            return false;
        }

        exitCode = RunAsync(args).GetAwaiter().GetResult();
        return true;
    }

    internal static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var includeNetwork = !args.Contains("--no-network", StringComparer.Ordinal);
        var dataRoot = ResolveDataRoot(args);
        Directory.CreateDirectory(dataRoot);

        var infrastructure = new RelicSelfCheckRunner(dataRoot, includeNetwork);
        var report = await infrastructure.RunAsync(cancellationToken).ConfigureAwait(false);
        var items = report.Items.ToList();
        items.Add(ViewNavigationSelfCheck.Verify());

        var combined = new SelfCheckReport(items);
        WriteReport(combined, dataRoot);
        return combined.Passed ? 0 : 1;
    }

    internal static string ResolveDataRoot(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--self-check-data", StringComparison.Ordinal))
            {
                return SelfCheckEnvironment.ResolveDataRoot(args[i + 1]);
            }
        }

        return SelfCheckEnvironment.ResolveDataRoot();
    }

    private static void WriteReport(SelfCheckReport report, string dataRoot)
    {
        Console.WriteLine("Relic Launcher self-check");
        Console.WriteLine($"Data root: {dataRoot}");
        foreach (var item in report.Items)
        {
            var label = item.Status switch
            {
                SelfCheckStatus.Pass => "pass",
                SelfCheckStatus.Fail => "fail",
                SelfCheckStatus.Skip => "skip",
                _ => "unknown",
            };

            if (string.IsNullOrWhiteSpace(item.Detail))
            {
                Console.WriteLine($"[{label}] {item.Name}");
            }
            else
            {
                Console.WriteLine($"[{label}] {item.Name}: {item.Detail}");
            }
        }

        Console.WriteLine(
            $"Summary: {report.PassCount} passed, {report.FailCount} failed, {report.SkipCount} skipped");
    }
}
