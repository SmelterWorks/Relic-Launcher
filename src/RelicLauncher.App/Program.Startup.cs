using Microsoft.Extensions.DependencyInjection;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Infrastructure;
using RelicLauncher.Infrastructure.Sandbox;

namespace RelicLauncher.App;

internal static partial class Program
{
    private static int TryRunSandboxBroker(string[] args)
    {
        if (!args.Contains(SandboxBootstrap.BrokerArgument, StringComparer.Ordinal))
        {
            return -1;
        }

        var brokerServices = BuildServices();
        try
        {
            return SandboxBootstrap.RunBrokerAsync(args, brokerServices).GetAwaiter().GetResult();
        }
        finally
        {
            brokerServices.DisposeProvider();
        }
    }

    private static int TryRunSandboxBootstrap(string[] args)
    {
        try
        {
            var bootstrapProbe = BuildServices();
            try
            {
                var settingsStore = bootstrapProbe.GetRequiredService<ILauncherSettingsStore>();
                var pathProvider = bootstrapProbe.GetRequiredService<IAppPathProvider>();
                return SandboxBootstrap.TryBootstrapAsync(args, settingsStore, pathProvider).GetAwaiter().GetResult();
            }
            finally
            {
                bootstrapProbe.DisposeProvider();
            }
        }
        catch
        {
            return -1;
        }
    }
}
