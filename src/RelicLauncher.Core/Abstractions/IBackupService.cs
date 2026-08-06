using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IBackupService
{
    Task<Result<BackupSummary>> CreateAsync(BackupRequest request, CancellationToken cancellationToken = default);

    Task<Result<BackupRestoreSummary>> RestoreAsync(BackupRestoreRequest request, CancellationToken cancellationToken = default);

    Task<Result<BackupManifest>> ReadManifestAsync(string zipPath, CancellationToken cancellationToken = default);
}
