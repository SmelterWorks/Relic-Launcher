using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Sandbox;
using Xunit;

namespace RelicLauncher.Core.Tests.Sandbox;

public class SandboxNetGrantsTests
{
    [Fact]
    public void ForLauncherEndpoints_IncludesHttpsAndDnsPorts()
    {
        var grants = SandboxNetGrants.ForLauncherEndpoints(EndpointSettings.CreateDefaults());

        grants.Should().Contain(g => g.Port == 443 && g.AllowConnectTcp);
        grants.Should().Contain(g => g.Port == 80 && g.AllowConnectTcp);
        grants.Should().Contain(g => g.Port == 53 && g.AllowConnectSendUdp);
    }

    [Fact]
    public void ForLauncherEndpoints_IncludesCustomServicePort()
    {
        var endpoints = EndpointSettings.CreateDefaults();
        endpoints.WikiBaseUrl = "http://wiki.example.com:8123/";

        var grants = SandboxNetGrants.ForLauncherEndpoints(endpoints);

        grants.Should().Contain(g => g.Port == 8123 && g.AllowConnectTcp);
    }
}
