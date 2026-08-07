using System.Text.Json.Serialization;

namespace RelicLauncher.Infrastructure.Sandbox;

internal sealed class BrokerWriteFilePayload
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("base64")]
    public string Base64 { get; set; } = string.Empty;
}
