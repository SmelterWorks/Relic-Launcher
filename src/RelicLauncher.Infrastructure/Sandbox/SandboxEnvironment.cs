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
}
