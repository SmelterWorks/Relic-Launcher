using System.Collections.Generic;

namespace RelicLauncher.Infrastructure.Sandbox;

public static partial class SandboxBootstrap
{
    private static (string Executable, List<string> Args) BuildUiLaunchCommand(string[] args)
    {
        var uiExecutable = Environment.ProcessPath ?? "dotnet";
        var uiArgs = new List<string>();
        if (string.Equals(uiExecutable, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            uiExecutable = "dotnet";
            uiArgs.Add(Path.Combine(AppContext.BaseDirectory, "RelicLauncher.App.dll"));
        }

        foreach (var arg in args)
        {
            if (!string.Equals(arg, BrokerArgument, StringComparison.Ordinal))
            {
                uiArgs.Add(arg);
            }
        }

        return (uiExecutable, uiArgs);
    }
}
