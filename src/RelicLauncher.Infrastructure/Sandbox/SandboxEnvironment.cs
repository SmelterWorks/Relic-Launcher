using System.Collections;
using System.Diagnostics;

namespace RelicLauncher.Infrastructure.Sandbox;

public static class SandboxEnvironment
{
    public const string BrokerRole = "RELIC_BROKER_ROLE";
    public const string BrokerRoleValue = "broker";
    public const string UiRoleValue = "ui";
    public const string BrokerPipeName = "RELIC_BROKER_PIPE";
    public const string BrokerSocketPath = "RELIC_BROKER_SOCKET";
    public const string RunningSandboxed = "RELIC_RUNNING_SANDBOXED";
    public const string SkipBootstrap = "RELIC_SKIP_SANDBOX_BOOTSTRAP";

    public static IDictionary<string, string?> CreateChildEnvironment(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string key)
            {
                continue;
            }

            env[key] = entry.Value?.ToString();
        }

        if (overrides is null)
        {
            return env;
        }

        foreach (var (key, value) in overrides)
        {
            env[key] = value;
        }

        return env;
    }

    public static void ApplyToStartInfo(
        ProcessStartInfo startInfo,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        foreach (var (key, value) in CreateChildEnvironment(overrides))
        {
            startInfo.Environment[key] = value;
        }
    }
}
