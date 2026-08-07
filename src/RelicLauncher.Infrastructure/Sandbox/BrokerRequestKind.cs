namespace RelicLauncher.Infrastructure.Sandbox;

internal enum BrokerRequestKind
{
    LaunchSandboxed = 0,
    OpenDirectory = 1,
    OpenUrl = 2,
    RunInstaller = 3,
    WriteFile = 4,
    Ping = 5,
    GetStatus = 6,
    ReadProcessOutput = 7,
    WriteProcessInput = 8,
    KillProcess = 9,
}
