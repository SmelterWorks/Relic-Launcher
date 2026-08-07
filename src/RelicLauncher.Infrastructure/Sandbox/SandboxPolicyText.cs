using System.Text;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

internal static class SandboxPolicyText
{
    public static string Serialize(SandboxPolicy policy)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"kind={(int)policy.Kind}");
        builder.AppendLine($"scope_abstract={(policy.ScopeAbstractUnixSocket ? 1 : 0)}");
        builder.AppendLine($"scope_signal={(policy.ScopeSignal ? 1 : 0)}");
        builder.AppendLine($"seccomp={(int)policy.SeccompProfile}");
        builder.AppendLine($"max_abi={policy.MaxLandlockAbi}");

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
