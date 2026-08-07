using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

public sealed class LinuxSandboxLauncher
{
    private readonly ILogger<LinuxSandboxLauncher> _logger;
    private readonly string? _helperPath;

    public LinuxSandboxLauncher(ILogger<LinuxSandboxLauncher> logger)
    {
        _logger = logger;
        _helperPath = ResolveHelperPathStatic();
    }

    public bool IsHelperAvailable => _helperPath is not null && File.Exists(_helperPath);

    public Result<ProcessStartInfo> BuildStartInfo(
        SandboxPolicy policy,
        string executablePath,
        IList<string> arguments,
        IDictionary<string, string?>? environment,
        string? workingDirectory,
        bool stdioPassthrough)
    {
        if (_helperPath is null)
        {
            return Result<ProcessStartInfo>.Failure("relic-sandbox helper was not found.");
        }

        var policyFile = Path.Combine(Path.GetTempPath(), $"relic-policy-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(policyFile, SandboxPolicyText.Serialize(policy));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<ProcessStartInfo>.Failure(ex.Message);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _helperPath,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
        };

        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(policyFile);
        if (stdioPassthrough)
        {
            startInfo.ArgumentList.Add("--stdio-passthrough");
        }

        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(executablePath);
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        return Result<ProcessStartInfo>.Success(startInfo);
    }

    public static int? ProbeLandlockAbi()
    {
        var helper = ResolveHelperPathStatic();
        if (helper is null)
        {
            return null;
        }

        try
        {
            using var process = global::System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = helper,
                ArgumentList = { "--self-check" },
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("landlock_abi=", StringComparison.Ordinal)
                    && int.TryParse(line.AsSpan("landlock_abi=".Length), out var abi))
                {
                    return abi;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public static bool ProbeSeccomp()
    {
        var helper = ResolveHelperPathStatic();
        if (helper is null)
        {
            return false;
        }

        try
        {
            using var process = global::System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = helper,
                ArgumentList = { "--self-check" },
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output.Contains("seccomp=1", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static string? ResolveHelperPathStatic()
    {
        var appDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDir, "relic-sandbox"),
            Path.Combine(appDir, "native", "relic-sandbox"),
            "/usr/lib/relic-launcher/relic-sandbox",
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
