using FluentAssertions;
using RelicLauncher.Infrastructure.Sandbox;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests.Sandbox;

public class SandboxEnvironmentTests
{
    [Fact]
    public void CreateChildEnvironment_CopiesCurrentProcessAndAppliesOverrides()
    {
        const string marker = "RELIC_SANDBOX_ENV_TEST";
        Environment.SetEnvironmentVariable(marker, "parent");

        try
        {
            var env = SandboxEnvironment.CreateChildEnvironment(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [SandboxEnvironment.RunningSandboxed] = "1",
                    [marker] = "child",
                });

            env[SandboxEnvironment.RunningSandboxed].Should().Be("1");
            env[marker].Should().Be("child");
            env.ContainsKey("PATH").Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(marker, null);
        }
    }
}
