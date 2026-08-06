using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IModpackService
{
    Task<Result<ModpackSummary>> ExportAsync(ModpackExportRequest request, CancellationToken cancellationToken = default);

    Task<Result<ModpackManifest>> ReadManifestAsync(string path, CancellationToken cancellationToken = default);

    Task<Result<ModpackApplyDiff>> ComputeApplyDiffAsync(
        ModpackApplyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ModpackApplySummary>> ApplyAsync(ModpackApplyRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ModpackLocalSummary>>> ListLocalAsync(CancellationToken cancellationToken = default);

    Task<Result<ModpackLocalSummary>> SaveLocalAsync(ModpackSaveRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteLocalAsync(string packId, CancellationToken cancellationToken = default);

    Task<Result<ModpackSummary>> ExportSavedAsync(
        string packDirectory,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
