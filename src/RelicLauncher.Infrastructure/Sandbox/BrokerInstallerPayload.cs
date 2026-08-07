using System.Text.Json.Serialization;

namespace RelicLauncher.Infrastructure.Sandbox;

internal sealed class BrokerInstallerPayload
{
    [JsonPropertyName("executable")]
    public string Executable { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public List<string> Arguments { get; set; } = [];
}
