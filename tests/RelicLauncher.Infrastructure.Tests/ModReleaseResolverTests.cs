using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Mods;
using RelicLauncher.Testing;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModReleaseResolverTests
{
    [Fact]
    public async Task ResolveAsync_UsesV2InstallInformationWhenAvailable()
    {
        using var temp = new TempAppPaths();
        var handler = new StubHandler(request =>
        {
            request.RequestUri!.AbsolutePath.Should().Contain("/api/v2/mods/install-information");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"samplemod":{"fileName":"samplemod_2.0.0.zip","fileUrl":"/download/99/samplemod_2.0.0.zip","recommendedUpgrade":"2.0.0"}}}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });

        var resolver = new ModReleaseResolver(
            new ThrowingModDbClient(),
            new EndpointProvider(),
            NullLogger<ModReleaseResolver>.Instance,
            new HttpClient(handler));

        var result = await resolver.ResolveAsync("samplemod", "1.22.6");

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileId.Should().Be(99);
        result.Value.ModVersion.Should().Be("2.0.0");
        result.Value.FileName.Should().Be("samplemod_2.0.0.zip");
        result.Value.DownloadUrl.Should().Contain("download/99/");
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToV1CompatibleTags()
    {
        using var temp = new TempAppPaths();
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var modDb = new StubModDbClient(new ModDetails
        {
            ModId = 1,
            UrlAlias = "samplemod",
            Name = "Sample",
            Releases =
            [
                new ModReleaseInfo
                {
                    FileId = 11,
                    ModVersion = "1.0.0",
                    FileName = "sample_1.0.0.zip",
                    CompatibleGameVersions = ["1.21.0"],
                    DownloadUrl = "https://example.test/a",
                },
                new ModReleaseInfo
                {
                    FileId = 22,
                    ModVersion = "1.5.0",
                    FileName = "sample_1.5.0.zip",
                    CompatibleGameVersions = ["1.22.0", "1.22.6"],
                    DownloadUrl = "https://example.test/b",
                },
            ],
        });

        var resolver = new ModReleaseResolver(
            modDb,
            new EndpointProvider(),
            NullLogger<ModReleaseResolver>.Instance,
            new HttpClient(handler));

        var result = await resolver.ResolveAsync("samplemod", "1.22.6");

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileId.Should().Be(22);
        result.Value.ModVersion.Should().Be("1.5.0");
    }

    [Fact]
    public async Task ResolveAsync_FailsWhenNoReleaseMatchesGameVersion()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        var modDb = new StubModDbClient(new ModDetails
        {
            ModId = 1,
            Name = "Sample",
            Releases =
            [
                new ModReleaseInfo
                {
                    FileId = 11,
                    ModVersion = "1.0.0",
                    CompatibleGameVersions = ["1.20.0"],
                    DownloadUrl = "https://example.test/a",
                },
            ],
        });

        var resolver = new ModReleaseResolver(
            modDb,
            new EndpointProvider(),
            NullLogger<ModReleaseResolver>.Instance,
            new HttpClient(handler));

        var result = await resolver.ResolveAsync("samplemod", "1.22.6");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("1.22.6");
    }

    private sealed class StubModDbClient(ModDetails details) : IModDbClient
    {
        public Task PrefetchCatalogAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Result<IReadOnlyList<ModTagInfo>>> GetTagsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<ModTagInfo>>.Success(Array.Empty<ModTagInfo>()));

        public Task<Result<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ModSearchResult>.Failure("not used"));

        public Task<Result<ModDetails>> GetModAsync(string modIdOrAlias, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ModDetails>.Success(details));

        public Task<Result<IReadOnlyList<ModSummary>>> GetCatalogAsync(
            bool preferCache = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<ModSummary>>.Success(Array.Empty<ModSummary>()));
    }

    private sealed class ThrowingModDbClient : IModDbClient
    {
        public Task PrefetchCatalogAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Result<IReadOnlyList<ModTagInfo>>> GetTagsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<ModTagInfo>>.Success(Array.Empty<ModTagInfo>()));

        public Task<Result<ModSearchResult>> SearchAsync(ModSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<ModSearchResult>.Failure("not used"));

        public Task<Result<ModDetails>> GetModAsync(string modIdOrAlias, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("v1 fallback should not run when v2 succeeds");

        public Task<Result<IReadOnlyList<ModSummary>>> GetCatalogAsync(
            bool preferCache = true,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("catalog not used");
    }
}
