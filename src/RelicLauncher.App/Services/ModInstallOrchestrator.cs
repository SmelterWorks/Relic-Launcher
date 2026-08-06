using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.Services;

public sealed class ModInstallOrchestrator
{
    private readonly IModLibraryService _modLibrary;
    private readonly IModBlocklistService _blocklist;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly ITransferTracker _transfers;
    private readonly ILogger<ModInstallOrchestrator> _logger;

    public ModInstallOrchestrator(
        IModLibraryService modLibrary,
        IModBlocklistService blocklist,
        IConfirmDialogService confirmDialog,
        ITransferTracker transfers,
        ILogger<ModInstallOrchestrator> logger)
    {
        _modLibrary = modLibrary;
        _blocklist = blocklist;
        _confirmDialog = confirmDialog;
        _transfers = transfers;
        _logger = logger;
    }

    public async Task<bool> ConfirmBlockedReleaseAsync(LauncherSettings settings, string modId, ModReleaseInfo release)
    {
        if (!settings.WarnOnBlockedMods)
        {
            return true;
        }

        var match = await _blocklist.FindMatchAsync(modId, release.ModVersion).ConfigureAwait(false);
        if (!match.IsSuccess || match.Value is null)
        {
            return true;
        }

        var reason = string.IsNullOrWhiteSpace(match.Value.Reason)
            ? match.Value.Id
            : $"{match.Value.Id}: {match.Value.Reason}";
        return await _confirmDialog.ConfirmAsync(
            "Blocked mod warning",
            $"This release is on the official Vintage Story blocked-mods list ({reason}). Install anyway?",
            "Install anyway",
            "Cancel").ConfigureAwait(false);
    }

    public async Task<bool> ConfirmDependencyPlanAsync(ModDependencyInstallPlan plan)
    {
        var extras = plan.ReleasesToInstall
            .Where(s => s.Depth > 0 && s.Release is not null)
            .ToList();
        var unresolved = plan.Unresolved;
        if (extras.Count == 0 && unresolved.Count == 0)
        {
            return true;
        }

        var lines = new List<string>();
        if (extras.Count > 0)
        {
            lines.Add("Also install these dependencies:");
            foreach (var step in extras)
            {
                var version = step.Release?.ModVersion ?? "?";
                lines.Add($"- {step.ModId} {version}");
            }
        }

        if (unresolved.Count > 0)
        {
            lines.Add("Could not resolve:");
            foreach (var step in unresolved)
            {
                lines.Add($"- {step.ModId}: {step.Error ?? "unavailable"}");
            }

            lines.Add("Install the selected mod anyway?");
        }

        return await _confirmDialog.ConfirmAsync(
            extras.Count > 0 ? "Install dependencies" : "Unresolved dependencies",
            string.Join(Environment.NewLine, lines),
            "Install",
            "Cancel").ConfigureAwait(false);
    }

    public async Task<bool> ConfirmBlockedPlanAsync(LauncherSettings settings, ModDependencyInstallPlan plan)
    {
        foreach (var step in plan.ReleasesToInstall)
        {
            if (step.Release is null)
            {
                continue;
            }

            if (!await ConfirmBlockedReleaseAsync(settings, step.ModId, step.Release).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<ModInstallResult> InstallPlanAsync(
        string dataPath,
        ModDependencyInstallPlan plan,
        Func<ModDependencyInstallStep, string> displayNameResolver)
    {
        foreach (var step in plan.ReleasesToInstall)
        {
            if (step.Release is null)
            {
                continue;
            }

            var displayName = displayNameResolver(step);
            var result = await InstallReleaseAsync(dataPath, displayName, step.Release).ConfigureAwait(false);
            if (!result.Success)
            {
                return result;
            }
        }

        return ModInstallResult.Ok();
    }

    public async Task<ModInstallResult> InstallReleaseAsync(string dataPath, string displayName, ModReleaseInfo release)
    {
        var jobId = $"mod-{release.FileId}-{Guid.NewGuid():N}";
        var session = _transfers.Begin(jobId, $"Mod {displayName}", TransferJobKind.Mod);
        try
        {
            await session.StartAsync().ConfigureAwait(true);

            var progress = new Progress<double>(session.Report);
            var result = await _modLibrary.InstallAsync(dataPath, release, progress).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                session.Fail(result.Error ?? "Install failed.");
                _logger.LogWarning("Mod install failed: {Error}", result.Error);
                return ModInstallResult.Fail(result.Error ?? "Install failed.");
            }

            session.Complete($"Installed {result.Value!.FileName}");
            return ModInstallResult.Ok($"Installed {result.Value.FileName}");
        }
        catch (OperationCanceledException)
        {
            session.Cancel();
            return ModInstallResult.Cancel();
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
