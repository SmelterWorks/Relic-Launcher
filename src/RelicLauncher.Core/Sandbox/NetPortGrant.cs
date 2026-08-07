namespace RelicLauncher.Core.Sandbox;

public sealed class NetPortGrant
{
    public ushort Port { get; init; }

    public bool AllowBindTcp { get; init; }

    public bool AllowConnectTcp { get; init; }

    public bool AllowBindUdp { get; init; }

    public bool AllowConnectSendUdp { get; init; }
}
