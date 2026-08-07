using RelicLauncher.App;
using RelicLauncher.Infrastructure.Sandbox;

namespace RelicLauncher.Bootstrap;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable(SandboxEnvironment.BrokerRole, SandboxEnvironment.BrokerRoleValue);

        using var services = RelicLauncher.App.Program.BuildServices();
        return SandboxBootstrap.RunBrokerAsync(args, services).GetAwaiter().GetResult();
    }
}
