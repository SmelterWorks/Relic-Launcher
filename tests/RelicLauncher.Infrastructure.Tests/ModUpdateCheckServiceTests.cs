using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModUpdateCheckServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_FindsNewerModDbRelease()
    {
        using var temp = new TempAppPaths();
        var library = new StubModLibrary(
        [
            new LocalModInfo
            {
                Path = "/mods/sample_1.0.0.zip",
                FileName = "mod_10.zip",
                ModId = "sample",
                Name = "Sample",
                Version = "1.0.0",
                IsEnabled = true,
            },
        ]);
        var resolver = new StubReleaseResolver(new ModReleaseInfo
        {
            FileId = 20,
            ModVersion = "2.0.0",
            FileName = "sample_2.0.0.zip",
            CompatibleGameVersions = ["1.22.6"],
            DownloadUrl = "https://example.test/download?fileid=20",
        });
        var origin = new StubOriginResolver(ModpackModSource.ModDb, 10);
        var state = new InMemoryModUpdateStateStore();
        var service = CreateService(library, resolver, origin, state);

        var result = await service.CheckForUpdatesAsync("/data", "1.22.6", [], force: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Candidates.Should().ContainSingle();
        result.Value.Candidates[0].AvailableVersion.Should().Be("2.0.0");
        result.Value.Candidates[0].InstalledVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SkipsOptOutMods()
    {
        using var temp = new TempAppPaths();
        var library = new StubModLibrary(
        [
            new LocalModInfo
            {
                Path = "/mods/sample_1.0.0.zip",
                FileName = "mod_10.zip",
                ModId = "sample",
                Name = "Sample",
                Version = "1.0.0",
                IsEnabled = true,
            },
        ]);
        var resolver = new StubReleaseResolver(new ModReleaseInfo
        {
            FileId = 20,
            ModVersion = "2.0.0",
            FileName = "sample_2.0.0.zip",
            CompatibleGameVersions = ["1.22.6"],
            DownloadUrl = "https://example.test/download?fileid=20",
        });
        var origin = new StubOriginResolver(ModpackModSource.ModDb, 10);
        var state = new InMemoryModUpdateStateStore();
        var service = CreateService(library, resolver, origin, state);

        var result = await service.CheckForUpdatesAsync("/data", "1.22.6", ["sample"], force: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Candidates.Should().BeEmpty();
        result.Value.SkippedOptOutCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SkipsLocalOnlyMods()
    {
        using var temp = new TempAppPaths();
        var library = new StubModLibrary(
        [
            new LocalModInfo
            {
                Path = "/mods/local.zip",
                FileName = "local.zip",
                ModId = "localmod",
                Name = "Local",
                Version = "1.0.0",
                IsEnabled = true,
            },
        ]);
        var resolver = new StubReleaseResolver(new ModReleaseInfo
        {
            FileId = 20,
            ModVersion = "2.0.0",
            FileName = "local_2.0.0.zip",
            CompatibleGameVersions = ["1.22.6"],
            DownloadUrl = "https://example.test/download?fileid=20",
        });
        var origin = new StubOriginResolver(ModpackModSource.Local, 0);
        var state = new InMemoryModUpdateStateStore();
        var service = CreateService(library, resolver, origin, state);

        var result = await service.CheckForUpdatesAsync("/data", "1.22.6", [], force: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Candidates.Should().BeEmpty();
        result.Value.SkippedLocalOnlyCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_UsesThrottleUnlessForced()
    {
        using var temp = new TempAppPaths();
        var library = new StubModLibrary([]);
        var resolver = new StubReleaseResolver(new ModReleaseInfo
        {
            FileId = 1,
            ModVersion = "1.0.0",
            FileName = "a.zip",
            CompatibleGameVersions = ["1.22.6"],
            DownloadUrl = "https://example.test/a",
        });
        var origin = new StubOriginResolver(ModpackModSource.ModDb, 1);
        var state = new InMemoryModUpdateStateStore();
        var service = CreateService(library, resolver, origin, state);

        var primed = await service.CheckForUpdatesAsync("/data", "1.22.6", [], force: true);
        primed.IsSuccess.Should().BeTrue();

        var throttled = await service.CheckForUpdatesAsync("/data", "1.22.6", [], force: false);
        throttled.IsSuccess.Should().BeTrue();
        throttled.Value!.WasThrottled.Should().BeTrue();

        var forced = await service.CheckForUpdatesAsync("/data", "1.22.6", [], force: true);
        forced.IsSuccess.Should().BeTrue();
        forced.Value!.WasThrottled.Should().BeFalse();
    }

    [Theory]
    [InlineData("1.0.0", 10, "2.0.0", 20, true)]
    [InlineData("2.0.0", 20, "2.0.0", 20, false)]
    [InlineData("2.0.0", 20, "1.5.0", 15, false)]
    public void IsUpdateAvailable_ComparesVersionAndFileId(
        string installedVersion,
        int installedFileId,
        string availableVersion,
        int availableFileId,
        bool expected)
    {
        var mod = new LocalModInfo
        {
            Path = "/mods/a.zip",
            FileName = "a.zip",
            ModId = "a",
            Version = installedVersion,
            IsEnabled = true,
        };
        var release = new ModReleaseInfo
        {
            FileId = availableFileId,
            ModVersion = availableVersion,
            FileName = "a.zip",
            CompatibleGameVersions = ["1.22.6"],
            DownloadUrl = "https://example.test/a",
        };

        ModUpdateCheckService.IsUpdateAvailable(mod, installedFileId, release).Should().Be(expected);
    }

    [Fact]
    public void JsonModUpdateStateStore_PersistsRecentlyUpdated()
    {
        using var temp = new TempAppPaths();
        var store = new JsonModUpdateStateStore(new FixedPathProvider(temp.Paths));

        store.MarkRecentlyUpdated("sample", "2.0.0");
        store.GetRecentlyUpdatedMods().Should().ContainKey("sample").WhoseValue.Should().Be("2.0.0");

        store.ClearRecentlyUpdated("sample");
        store.GetRecentlyUpdatedMods().Should().BeEmpty();
    }

    private static ModUpdateCheckService CreateService(
        IModLibraryService library,
        IModReleaseResolver resolver,
        IModOriginResolver origin,
        IModUpdateStateStore state)
        => new(library, resolver, origin, state, NullLogger<ModUpdateCheckService>.Instance);

    private sealed class StubModLibrary(IReadOnlyList<LocalModInfo> mods) : IModLibraryService
    {
        public Task<Result<IReadOnlyList<LocalModInfo>>> ListInstalledAsync(string dataPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<LocalModInfo>>.Success(mods));

        public Task<Result<LocalModInfo>> InstallAsync(string dataPath, ModReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> UninstallAsync(LocalModInfo mod, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<LocalModInfo>> SetEnabledAsync(LocalModInfo mod, bool enabled, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<int>> CleanDuplicateModsAsync(string dataPath, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<LocalModInfo>> ImportLocalAsync(string dataPath, string sourcePath, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<string>> EnsureReleaseCachedAsync(ModReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ParsedModInfo? TryPeekModInfo(string zipOrFolderPath) => null;

        public byte[]? TryReadModIcon(LocalModInfo mod) => null;
    }

    private sealed class StubReleaseResolver(ModReleaseInfo release) : IModReleaseResolver
    {
        public Task<Result<ModReleaseInfo>> ResolveAsync(string modIdentifier, string gameVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ModReleaseInfo>.Success(release));
    }

    private sealed class StubOriginResolver(ModpackModSource source, int fileId) : IModOriginResolver
    {
        public ModOriginInfo Resolve(LocalModInfo mod)
            => new() { Source = source, FileId = fileId };

        public IReadOnlyList<ModFileIndexEntry> GetIndexEntries() => [];
    }

    private sealed class InMemoryModUpdateStateStore : IModUpdateStateStore
    {
        public DateTimeOffset? LastCheckUtc { get; set; }

        private readonly Dictionary<string, string> _recent = new(StringComparer.OrdinalIgnoreCase);

        public DateTimeOffset? GetLastCheckUtc() => LastCheckUtc;

        public void SetLastCheckUtc(DateTimeOffset value) => LastCheckUtc = value;

        public IReadOnlyDictionary<string, string> GetRecentlyUpdatedMods()
            => new Dictionary<string, string>(_recent, StringComparer.OrdinalIgnoreCase);

        public void MarkRecentlyUpdated(string modId, string version) => _recent[modId] = version;

        public void ClearRecentlyUpdated(string modId) => _recent.Remove(modId);

        public void ClearAllRecentlyUpdated() => _recent.Clear();
    }
}
