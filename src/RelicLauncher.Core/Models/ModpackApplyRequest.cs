namespace RelicLauncher.Core.Models;

public sealed class ModpackApplyRequest
{
    public required string DataPath { get; init; }
    public required ModpackManifest Manifest { get; init; }
    public string? ZipPath { get; init; }
    public required ModpackApplyMode Mode { get; init; }
    public IProgress<double>? Progress { get; init; }
}
