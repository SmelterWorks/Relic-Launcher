using System.Text.Json.Serialization;

namespace RelicLauncher.Infrastructure.Sandbox;

internal sealed class BrokerResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("pid")]
    public int? ProcessId { get; set; }

    [JsonPropertyName("sandboxed")]
    public bool Sandboxed { get; set; }

    [JsonPropertyName("degradedReason")]
    public string? DegradedReason { get; set; }

    [JsonPropertyName("statusJson")]
    public string? StatusJson { get; set; }

    [JsonPropertyName("outputBase64")]
    public string? OutputBase64 { get; set; }
}
