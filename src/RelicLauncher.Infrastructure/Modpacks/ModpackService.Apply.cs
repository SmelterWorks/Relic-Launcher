using System.IO.Compression;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Modpacks;

public sealed partial class ModpackService
{
    public async Task<Result<ModpackApplyDiff>> ComputeApplyDiffAsync(
        ModpackApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DataPath))
        {
            return Result<ModpackApplyDiff>.Failure("Vintage Story data path is not configured.");
        }

        var installed = await _modLibrary.ListInstalledAsync(request.DataPath, cancellationToken).ConfigureAwait(false);
        if (!installed.IsSuccess)
        {
            return Result<ModpackApplyDiff>.Failure(installed.Error ?? "Could not list installed mods.");
        }

        var installedById = BuildInstalledMap(installed.Value!);
        var entries = BuildPackDiffEntries(request.Manifest, installedById);
        if (request.Mode == ModpackApplyMode.Replace)
        {
            AddRemoveDiffEntries(request.Manifest, installedById, entries);
        }

        return Result<ModpackApplyDiff>.Success(new ModpackApplyDiff { Entries = entries });
    }

    public async Task<Result<ModpackApplySummary>> ApplyAsync(ModpackApplyRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DataPath))
        {
            return Result<ModpackApplySummary>.Failure("Vintage Story data path is not configured.");
        }

        if (request.Manifest.Distribution == ModpackDistribution.Offline
            && string.IsNullOrWhiteSpace(request.ZipPath))
        {
            return Result<ModpackApplySummary>.Failure("Offline modpack requires a zip path or saved pack directory.");
        }

        var listed = await _modLibrary.ListInstalledAsync(request.DataPath, cancellationToken).ConfigureAwait(false);
        if (!listed.IsSuccess)
        {
            return Result<ModpackApplySummary>.Failure(listed.Error ?? "Could not list installed mods.");
        }

        var state = new ModpackApplyState { InstalledMods = listed.Value! };
        if (request.Mode == ModpackApplyMode.Replace)
        {
            await RemoveModsNotInPackAsync(request, state, cancellationToken).ConfigureAwait(false);
        }

        await ApplyPackModsAsync(request, state, cancellationToken).ConfigureAwait(false);
        request.Progress?.Report(1.0);

        return Result<ModpackApplySummary>.Success(new ModpackApplySummary
        {
            InstalledCount = state.InstalledCount,
            UpdatedCount = state.UpdatedCount,
            RemovedCount = state.RemovedCount,
            SkippedCount = state.SkippedCount,
            FailedCount = state.FailedCount,
            Errors = state.Errors,
        });
    }

    private static Dictionary<string, LocalModInfo> BuildInstalledMap(IReadOnlyList<LocalModInfo> installed)
        => installed
            .Where(m => !BuiltinModIds.IsBuiltin(m.ModId) && m.IsEnabled)
            .GroupBy(m => m.ModId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static List<ModpackApplyDiffEntry> BuildPackDiffEntries(
        ModpackManifest manifest,
        Dictionary<string, LocalModInfo> installedById)
    {
        var entries = new List<ModpackApplyDiffEntry>();
        foreach (var packMod in manifest.Mods)
        {
            if (BuiltinModIds.IsBuiltin(packMod.ModId))
            {
                continue;
            }

            if (installedById.TryGetValue(packMod.ModId, out var current))
            {
                entries.Add(VersionsMatch(current.Version, packMod.ModVersion)
                    ? new ModpackApplyDiffEntry
                    {
                        ModId = packMod.ModId,
                        CurrentVersion = current.Version,
                        PackVersion = packMod.ModVersion,
                        Kind = ModpackApplyDiffKind.Skip,
                    }
                    : new ModpackApplyDiffEntry
                    {
                        ModId = packMod.ModId,
                        CurrentVersion = current.Version,
                        PackVersion = packMod.ModVersion,
                        Kind = ModpackApplyDiffKind.Update,
                    });
            }
            else
            {
                entries.Add(new ModpackApplyDiffEntry
                {
                    ModId = packMod.ModId,
                    PackVersion = packMod.ModVersion,
                    Kind = ModpackApplyDiffKind.Add,
                });
            }
        }

        return entries;
    }

    private static void AddRemoveDiffEntries(
        ModpackManifest manifest,
        Dictionary<string, LocalModInfo> installedById,
        List<ModpackApplyDiffEntry> entries)
    {
        var packIds = manifest.Mods
            .Where(m => !BuiltinModIds.IsBuiltin(m.ModId))
            .Select(m => m.ModId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in installedById)
        {
            if (!packIds.Contains(pair.Key))
            {
                entries.Add(new ModpackApplyDiffEntry
                {
                    ModId = pair.Key,
                    CurrentVersion = pair.Value.Version,
                    Kind = ModpackApplyDiffKind.Remove,
                });
            }
        }
    }

    private async Task RemoveModsNotInPackAsync(
        ModpackApplyRequest request,
        ModpackApplyState state,
        CancellationToken cancellationToken)
    {
        var packModIds = request.Manifest.Mods
            .Where(m => !BuiltinModIds.IsBuiltin(m.ModId))
            .Select(m => m.ModId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in state.InstalledMods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BuiltinModIds.IsBuiltin(mod.ModId) || packModIds.Contains(mod.ModId!))
            {
                continue;
            }

            var removed = await _modLibrary.UninstallAsync(mod, cancellationToken).ConfigureAwait(false);
            if (removed.IsSuccess)
            {
                state.RemovedCount++;
            }
            else
            {
                state.FailedCount++;
                state.Errors.Add($"{mod.ModId}: {removed.Error}");
            }

            state.CompletedSteps++;
            ReportProgress(request.Progress, state.CompletedSteps / Math.Max(1, state.TotalSteps));
        }
    }

    private async Task ApplyPackModsAsync(
        ModpackApplyRequest request,
        ModpackApplyState state,
        CancellationToken cancellationToken)
    {
        state.TotalSteps = request.Manifest.Mods.Count
            + (request.Mode == ModpackApplyMode.Replace ? state.InstalledMods.Count : 0);

        foreach (var packMod in request.Manifest.Mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BuiltinModIds.IsBuiltin(packMod.ModId))
            {
                state.SkippedCount++;
                continue;
            }

            await ApplySinglePackModAsync(request, packMod, state, cancellationToken).ConfigureAwait(false);
            state.CompletedSteps++;
            ReportProgress(request.Progress, state.CompletedSteps / Math.Max(1, state.TotalSteps));

            var refresh = await _modLibrary.ListInstalledAsync(request.DataPath, cancellationToken).ConfigureAwait(false);
            if (refresh.IsSuccess)
            {
                state.InstalledMods = refresh.Value!;
            }
        }
    }

    private async Task ApplySinglePackModAsync(
        ModpackApplyRequest request,
        ModpackModEntry packMod,
        ModpackApplyState state,
        CancellationToken cancellationToken)
    {
        var existing = state.InstalledMods.FirstOrDefault(m =>
            string.Equals(m.ModId, packMod.ModId, StringComparison.OrdinalIgnoreCase));
        var needsInstall = existing is null || !VersionsMatch(existing.Version, packMod.ModVersion);

        if (!needsInstall)
        {
            if (existing is not null)
            {
                await ApplyEnabledStateAsync(existing, packMod.Enabled, cancellationToken).ConfigureAwait(false);
            }

            state.SkippedCount++;
            return;
        }

        var wasUpdate = existing is not null;
        var applyResult = request.Manifest.Distribution == ModpackDistribution.Online
            ? await ApplyOnlineModAsync(request, packMod, request.Manifest.GameVersion, state.InstalledMods, cancellationToken).ConfigureAwait(false)
            : await ApplyOfflineModAsync(request, packMod, cancellationToken).ConfigureAwait(false);

        if (!applyResult.IsSuccess)
        {
            state.FailedCount++;
            state.Errors.Add($"{packMod.ModId}: {applyResult.Error}");
            return;
        }

        if (wasUpdate)
        {
            state.UpdatedCount++;
        }
        else
        {
            state.InstalledCount++;
        }

        if (applyResult.Value is not null)
        {
            await ApplyEnabledStateAsync(applyResult.Value, packMod.Enabled, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Result<LocalModInfo>> ApplyOnlineModAsync(
        ModpackApplyRequest request,
        ModpackModEntry packMod,
        string gameVersion,
        IReadOnlyList<LocalModInfo> installedMods,
        CancellationToken cancellationToken)
    {
        var identifier = packMod.FileId > 0
            ? packMod.FileId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : packMod.ModId;

        var resolved = await _releaseResolver.ResolveAsync(identifier, gameVersion, cancellationToken).ConfigureAwait(false);
        if (!resolved.IsSuccess)
        {
            resolved = await _releaseResolver.ResolveAsync(packMod.ModId, gameVersion, cancellationToken).ConfigureAwait(false);
        }

        if (!resolved.IsSuccess)
        {
            return Result<LocalModInfo>.Failure(resolved.Error ?? "Could not resolve mod release.");
        }

        var plan = await _dependencyPlanner.PlanAsync(resolved.Value!, gameVersion, installedMods, cancellationToken).ConfigureAwait(false);
        if (!plan.IsSuccess)
        {
            return Result<LocalModInfo>.Failure(plan.Error ?? "Could not resolve dependencies.");
        }

        LocalModInfo? lastInstalled = null;
        foreach (var step in plan.Value!.ReleasesToInstall)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (step.Release is null)
            {
                continue;
            }

            var install = await _modLibrary.InstallAsync(request.DataPath, step.Release, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!install.IsSuccess)
            {
                return Result<LocalModInfo>.Failure(install.Error ?? "Mod install failed.");
            }

            lastInstalled = install.Value;
        }

        return lastInstalled is null
            ? Result<LocalModInfo>.Failure("No release was installed.")
            : Result<LocalModInfo>.Success(lastInstalled);
    }

    private async Task<Result<LocalModInfo>> ApplyOfflineModAsync(
        ModpackApplyRequest request,
        ModpackModEntry packMod,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packMod.ArchivePath))
        {
            return Result<LocalModInfo>.Failure("Offline mod entry has no archive path.");
        }

        var sourceRoot = request.ZipPath!;
        string archivePath;
        if (Directory.Exists(sourceRoot))
        {
            archivePath = Path.Combine(sourceRoot, packMod.ArchivePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(archivePath))
            {
                return Result<LocalModInfo>.Failure($"Embedded mod archive missing: {packMod.ArchivePath}");
            }
        }
        else
        {
            using var archive = ZipFile.OpenRead(sourceRoot);
            var entry = archive.GetEntry(packMod.ArchivePath)
                        ?? archive.Entries.FirstOrDefault(e =>
                            string.Equals(e.FullName, packMod.ArchivePath, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return Result<LocalModInfo>.Failure($"Embedded mod archive missing: {packMod.ArchivePath}");
            }

            archivePath = Path.Combine(Path.GetTempPath(), $"relic-modpack-mod-{Guid.NewGuid():N}.zip");
            entry.ExtractToFile(archivePath, overwrite: true);
        }

        try
        {
            return await _modLibrary.ImportLocalAsync(request.DataPath, archivePath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!Directory.Exists(sourceRoot))
            {
                TryDelete(archivePath);
            }
        }
    }

    private async Task ApplyEnabledStateAsync(LocalModInfo mod, bool enabled, CancellationToken cancellationToken)
    {
        if (mod.IsEnabled == enabled)
        {
            return;
        }

        await _modLibrary.SetEnabledAsync(mod, enabled, cancellationToken).ConfigureAwait(false);
    }

    private sealed class ModpackApplyState
    {
        public IReadOnlyList<LocalModInfo> InstalledMods { get; set; } = [];
        public int InstalledCount { get; set; }
        public int UpdatedCount { get; set; }
        public int RemovedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; } = [];
        public int TotalSteps { get; set; }
        public int CompletedSteps { get; set; }
    }
}
