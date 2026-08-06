using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Transfers;
using Xunit;

namespace RelicLauncher.App.Tests;

public class ModInstallOrchestratorTests
{
    [Fact]
    public async Task ConfirmBlockedReleaseAsync_SkipsWhenWarnDisabled()
    {
        var orchestrator = CreateOrchestrator(new StubModLibrary(), new StubBlocklist(), new StubConfirmDialog(true));
        var settings = new LauncherSettings { WarnOnBlockedMods = false };
        var release = new ModReleaseInfo
        {
            FileId = 1,
            ModVersion = "1.0.0",
            DownloadUrl = "https://example.test/download?fileid=1",
        };

        var confirmed = await orchestrator.ConfirmBlockedReleaseAsync(settings, "blockedmod", release);

        confirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmDependencyPlanAsync_ReturnsFalse_WhenUserDeclines()
    {
        var orchestrator = CreateOrchestrator(new StubModLibrary(), new StubBlocklist(), new StubConfirmDialog(false));
        var plan = new ModDependencyInstallPlan
        {
            RootRelease = Release(1, "1.0.0"),
            RootModId = "root",
            Steps =
            [
                new ModDependencyInstallStep { ModId = "dep", Depth = 1, Release = Release(2, "1.0.0") },
            ],
        };

        var confirmed = await orchestrator.ConfirmDependencyPlanAsync(plan);

        confirmed.Should().BeFalse();
    }

    [Fact]
    public async Task InstallReleaseAsync_CompletesTransferOnSuccess()
    {
        var library = new StubModLibrary { InstallSucceeds = true };
        var transfers = new TransferTracker();
        var orchestrator = CreateOrchestrator(library, new StubBlocklist(), new StubConfirmDialog(true), transfers);
        var release = Release(5, "1.0.0");

        var result = await orchestrator.InstallReleaseAsync("/data", "Test Mod", release);

        result.Success.Should().BeTrue();
        transfers.GetJobs().Should().ContainSingle(j =>
            j.Kind == TransferJobKind.Mod && j.State == TransferJobState.Completed);
    }

    [Fact]
    public async Task InstallPlanAsync_StopsOnFirstFailure()
    {
        var library = new StubModLibrary
        {
            InstallResults = new Queue<Result<LocalModInfo>>([
                Result<LocalModInfo>.Failure("download failed"),
            ]),
        };
        var orchestrator = CreateOrchestrator(library, new StubBlocklist(), new StubConfirmDialog(true));
        var plan = new ModDependencyInstallPlan
        {
            RootRelease = Release(1, "1.0.0"),
            RootModId = "root",
            Steps =
            [
                new ModDependencyInstallStep { ModId = "root", Depth = 0, Release = Release(1, "1.0.0") },
                new ModDependencyInstallStep { ModId = "dep", Depth = 1, Release = Release(2, "1.0.0") },
            ],
        };

        var result = await orchestrator.InstallPlanAsync("/data", plan, step => step.ModId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("download failed");
        library.InstallCalls.Should().Be(1);
    }

    private static ModInstallOrchestrator CreateOrchestrator(
        IModLibraryService library,
        IModBlocklistService blocklist,
        IConfirmDialogService confirmDialog,
        ITransferTracker? transfers = null)
        => new(
            library,
            blocklist,
            confirmDialog,
            transfers ?? new TransferTracker(),
            NullLogger<ModInstallOrchestrator>.Instance);

    private static ModReleaseInfo Release(int fileId, string version)
        => new()
        {
            FileId = fileId,
            ModVersion = version,
            FileName = $"mod_{fileId}.zip",
            DownloadUrl = $"https://example.test/download?fileid={fileId}",
        };

    private sealed class StubConfirmDialog(bool result) : IConfirmDialogService
    {
        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel")
            => Task.FromResult(result);
    }

    private sealed class StubBlocklist : IModBlocklistService
    {
        public Task<Result<IReadOnlyList<ModBlocklistEntry>>> GetEntriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<ModBlocklistEntry>>.Success([]));

        public Task<Result<ModBlocklistEntry?>> FindMatchAsync(string? modId, string? modVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ModBlocklistEntry?>.Success(new ModBlocklistEntry { Id = $"{modId}@{modVersion}", Reason = "test" }));
    }

    private sealed class StubModLibrary : IModLibraryService
    {
        public bool InstallSucceeds { get; init; }
        public Queue<Result<LocalModInfo>> InstallResults { get; init; } = new();
        public int InstallCalls { get; private set; }

        public Task<Result<IReadOnlyList<LocalModInfo>>> ListInstalledAsync(string dataPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<LocalModInfo>>.Success([]));

        public Task<Result<LocalModInfo>> InstallAsync(string dataPath, ModReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            InstallCalls++;
            if (InstallResults.Count > 0)
            {
                return Task.FromResult(InstallResults.Dequeue());
            }

            if (!InstallSucceeds)
            {
                return Task.FromResult(Result<LocalModInfo>.Failure("install failed"));
            }

            progress?.Report(1.0);
            return Task.FromResult(Result<LocalModInfo>.Success(new LocalModInfo
            {
                Path = Path.Combine(dataPath, "Mods", release.FileName ?? "mod.zip"),
                FileName = release.FileName ?? "mod.zip",
                ModId = "testmod",
                Version = release.ModVersion,
                IsEnabled = true,
            }));
        }

        public Task<Result> UninstallAsync(LocalModInfo mod, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result<LocalModInfo>> SetEnabledAsync(LocalModInfo mod, bool enabled, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<LocalModInfo>.Success(mod));

        public Task<Result<int>> CleanDuplicateModsAsync(string dataPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<int>.Success(0));

        public Task<Result<LocalModInfo>> ImportLocalAsync(string dataPath, string sourcePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<LocalModInfo>.Failure("not implemented"));

        public Task<Result<string>> EnsureReleaseCachedAsync(ModReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<string>.Failure("not implemented"));

        public RelicLauncher.Core.Mods.ParsedModInfo? TryPeekModInfo(string zipOrFolderPath) => null;

        public byte[]? TryReadModIcon(LocalModInfo mod) => null;
    }
}
