using System.Text.Json.Serialization;

namespace RelicLauncher.Infrastructure.Sandbox;

internal sealed class BrokerLaunchPayload
{
    [JsonPropertyName("kind")]
    public int SandboxKind { get; set; }

    [JsonPropertyName("executable")]
    public string Executable { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public List<string> Arguments { get; set; } = [];

    [JsonPropertyName("environment")]
    public Dictionary<string, string?> Environment { get; set; } =
        new(StringComparer.Ordinal);

    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; set; }

    [JsonPropertyName("redirectStdin")]
    public bool RedirectStandardInput { get; set; }

    [JsonPropertyName("redirectStdout")]
    public bool RedirectStandardOutput { get; set; }

    [JsonPropertyName("redirectStderr")]
    public bool RedirectStandardError { get; set; }

    [JsonPropertyName("policyJson")]
    public string? PolicyJson { get; set; }
}
