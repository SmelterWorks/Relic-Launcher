namespace RelicLauncher.Core.Models;

public sealed class BackupRestoreRequest
{
    public required string SourceZipPath { get; init; }
    public string? DataPath { get; init; }
    public string? InstallsRoot { get; init; }
    public IProgress<double>? Progress { get; init; }
}
