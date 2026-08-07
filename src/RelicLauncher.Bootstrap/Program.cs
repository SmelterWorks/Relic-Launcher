using RelicLauncher.App;
using RelicLauncher.Infrastructure;
using RelicLauncher.Infrastructure.Sandbox;

namespace RelicLauncher.Bootstrap;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable(SandboxEnvironment.BrokerRole, SandboxEnvironment.BrokerRoleValue);

        var services = RelicLauncher.App.Program.BuildServices();
        try
        {
            return SandboxBootstrap.RunBrokerAsync(args, services).GetAwaiter().GetResult();
        }
        finally
        {
            services.DisposeProvider();
        }
    }
}
