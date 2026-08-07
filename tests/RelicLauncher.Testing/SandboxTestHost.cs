using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Infrastructure.Sandbox;
using RelicLauncher.Infrastructure.Server;

namespace RelicLauncher.Testing;

public static class SandboxTestHost
{
    public static GameServerHost CreateGameServerHost(IDotNetRuntimeProvisioner provisioner)
    {
        var broker = new TestSandboxBrokerClient();
        return new GameServerHost(
            provisioner,
            broker,
            new TestSandboxSupport(),
            new BrokerServerConsole(broker, NullLogger<BrokerServerConsole>.Instance),
            NullLogger<GameServerHost>.Instance);
    }
}
