using FluentAssertions;
using RelicLauncher.Core;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class BuildMetadataTests
{
    [Fact]
    public void Version_UsesThreePartFormat()
    {
        BuildMetadata.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
    }

    [Fact]
    public void CommitSha_IsNotBlank()
    {
        BuildMetadata.CommitSha.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildTimeUtc_IsNotBlank()
    {
        BuildMetadata.BuildTimeUtc.Should().NotBeNullOrWhiteSpace();
    }
}
