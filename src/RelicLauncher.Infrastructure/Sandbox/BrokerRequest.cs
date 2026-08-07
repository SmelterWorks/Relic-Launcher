using System.Text.Json.Serialization;

namespace RelicLauncher.Infrastructure.Sandbox;

internal sealed class BrokerRequest
{
    [JsonPropertyName("kind")]
    public BrokerRequestKind Kind { get; set; }

    [JsonPropertyName("launch")]
    public BrokerLaunchPayload? Launch { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("installer")]
    public BrokerInstallerPayload? Installer { get; set; }

    [JsonPropertyName("writeFile")]
    public BrokerWriteFilePayload? WriteFile { get; set; }

    [JsonPropertyName("processId")]
    public int? ProcessId { get; set; }

    [JsonPropertyName("inputBase64")]
    public string? InputBase64 { get; set; }
}
