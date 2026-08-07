using System.Text.Json.Serialization;

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

internal sealed class BrokerInstallerPayload
{
    [JsonPropertyName("executable")]
    public string Executable { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public List<string> Arguments { get; set; } = [];
}

internal sealed class BrokerWriteFilePayload
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("base64")]
    public string Base64 { get; set; } = string.Empty;
}

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
