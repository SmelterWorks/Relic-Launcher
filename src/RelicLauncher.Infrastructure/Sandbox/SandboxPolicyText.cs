using System.Globalization;
using System.Text;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

internal static class SandboxPolicyText
{
    public static string Serialize(SandboxPolicy policy)
    {
        var invariant = CultureInfo.InvariantCulture;
        var builder = new StringBuilder();
        builder.AppendLine(string.Create(invariant, $"kind={(int)policy.Kind}"));
        builder.AppendLine(string.Create(invariant, $"scope_abstract={(policy.ScopeAbstractUnixSocket ? 1 : 0)}"));
        builder.AppendLine(string.Create(invariant, $"scope_signal={(policy.ScopeSignal ? 1 : 0)}"));
        builder.AppendLine(string.Create(invariant, $"seccomp={(int)policy.SeccompProfile}"));
        builder.AppendLine(string.Create(invariant, $"max_abi={policy.MaxLandlockAbi}"));

        foreach (var grant in policy.PathGrants)
        {
            var token = grant.Access switch
            {
                PathAccess.ReadWrite => "RW",
                PathAccess.ReadExecute => "RX",
                _ => "RO",
            };
            builder.AppendLine($"{token} {grant.Path}");
        }

        foreach (var net in policy.NetPortGrants)
        {
            if (net.AllowBindTcp)
            {
                builder.AppendLine($"NET_BIND_TCP {net.Port}");
            }

            if (net.AllowConnectTcp)
            {
                builder.AppendLine($"NET_CONNECT_TCP {net.Port}");
            }

            if (net.AllowBindUdp)
            {
                builder.AppendLine($"NET_BIND_UDP {net.Port}");
            }

            if (net.AllowConnectSendUdp)
            {
                builder.AppendLine($"NET_CONNECT_UDP {net.Port}");
            }
        }

        return builder.ToString();
    }
}
