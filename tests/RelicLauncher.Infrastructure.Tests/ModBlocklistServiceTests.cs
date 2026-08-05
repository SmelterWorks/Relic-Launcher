using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Mods;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class ModBlocklistServiceTests
{
    [Fact]
    public void Parse_ReadsIdAndReason()
    {
        var json = """
            [
              { "id": "waypointtogether@1.0.1", "reason": "Contains a unfixed vulnerability" },
              { "id": "other@2.0.0", "reason": "test" }
            ]
            """;

        var entries = ModBlocklistService.Parse(json);

        entries.Should().HaveCount(2);
        entries[0].ModId.Should().Be("waypointtogether");
        entries[0].Version.Should().Be("1.0.1");
        entries[0].Reason.Should().Contain("vulnerability");
    }

    [Fact]
    public void FindMatch_MatchesExactVersion()
    {
        var entries = new[]
        {
            new ModBlocklistEntry { Id = "waypointtogether@1.0.1", Reason = "bad" },
            new ModBlocklistEntry { Id = "other@2.0.0", Reason = "x" },
        };

        var hit = ModBlocklistService.FindMatch(entries, "waypointtogether", "1.0.1");
        hit.Should().NotBeNull();
        hit!.Reason.Should().Be("bad");

        ModBlocklistService.FindMatch(entries, "waypointtogether", "9.9.9").Should().BeNull();
    }
}
