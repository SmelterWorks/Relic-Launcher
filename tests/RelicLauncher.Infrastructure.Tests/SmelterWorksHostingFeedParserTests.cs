using FluentAssertions;
using RelicLauncher.Core.Server;
using RelicLauncher.Infrastructure.Hosting;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class SmelterWorksHostingFeedParserTests
{
    [Fact]
    public void Parse_ExtractsPlansFromRssItems()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <item>
                  <title>Ember</title>
                  <category>Friends</category>
                  <description><![CDATA[$10 / month. 4 GB RAM. 25 GB NVMe.]]></description>
                </item>
                <item>
                  <title>Forge</title>
                  <category>Modded</category>
                  <description>$15 / month and $150 / year. 8 GB RAM.</description>
                </item>
              </channel>
            </rss>
            """;

        var plans = SmelterWorksHostingFeedParser.Parse(xml);

        plans.Should().HaveCount(2);
        plans[0].Name.Should().Be("Ember");
        plans[0].Subtitle.Should().Be("Friends");
        plans[0].MonthlyPrice.Should().Be("$10 / month");
    }

    [Fact]
    public void Parse_NormalizesMarketingTitles()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <item>
                  <title>Friends: $10/mo (Coming soon)</title>
                  <category>Friends</category>
                  <description>$10 / month. A small world with friends. Light mods.</description>
                </item>
              </channel>
            </rss>
            """;

        var plans = SmelterWorksHostingFeedParser.Parse(xml);

        plans.Should().ContainSingle();
        plans[0].Name.Should().Be("Friends");
        plans[0].Subtitle.Should().BeNull();
        plans[0].MonthlyPrice.Should().Be("$10 / month");
    }

    [Fact]
    public void GetFallbackPlans_ReturnsFourPlansIncludingByos()
    {
        var plans = SmelterWorksHostingFeedService.GetFallbackPlans();

        plans.Should().HaveCount(4);
        plans.Should().Contain(p => p.Name == "Anchor" && p.Subtitle == "Bring Your Own Server");
    }
}
