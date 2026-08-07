using System.Text.Json;
using System.Text.Json.Serialization;
using RelicLauncher.Core.Sandbox;

namespace RelicLauncher.Infrastructure.Sandbox;

internal static class SandboxPolicyJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize(SandboxPolicy policy) =>
        JsonSerializer.Serialize(policy, Options);

    public static SandboxPolicy? Deserialize(string json) =>
        JsonSerializer.Deserialize<SandboxPolicy>(json, Options);
}
