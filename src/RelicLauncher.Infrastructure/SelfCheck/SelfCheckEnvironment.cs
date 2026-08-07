namespace RelicLauncher.Infrastructure.SelfCheck;

public static class SelfCheckEnvironment
{
    public const string DataRootVariable = "RELIC_SELF_CHECK_ROOT";

    public static string ResolveDataRoot(string? overridePath = null)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath.Trim());
        }

        var fromEnv = Environment.GetEnvironmentVariable(DataRootVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv.Trim());
        }

        return Path.Combine(
            Path.GetTempPath(),
            "RelicLauncher-self-check",
            Guid.NewGuid().ToString("N"));
    }
}
