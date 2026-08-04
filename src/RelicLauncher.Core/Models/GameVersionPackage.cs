namespace RelicLauncher.Core.Models;

public sealed class GameVersionPackage
{
    public required string PlatformKey { get; init; }
    public required string FileName { get; init; }
    public required string CdnUrl { get; init; }
    public string? LocalUrl { get; init; }
    public string? Md5 { get; init; }
    public string? FileSizeLabel { get; init; }
    public ClientPackageKind Kind { get; init; }
}
