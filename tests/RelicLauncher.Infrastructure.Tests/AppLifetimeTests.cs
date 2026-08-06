using FluentAssertions;
using RelicLauncher.Infrastructure.Hosting;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class AppLifetimeTests
{
    [Fact]
    public void RequestShutdown_CancelsApplicationStopping()
    {
        using var lifetime = new AppLifetime();
        var canceled = false;
        using var registration = lifetime.ApplicationStopping.Register(() => canceled = true);

        lifetime.RequestShutdown();

        canceled.Should().BeTrue();
        lifetime.ApplicationStopping.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void RequestShutdown_InvokesRegisteredHandler()
    {
        using var lifetime = new AppLifetime();
        var handlerCalled = false;
        lifetime.RegisterShutdownHandler(() => handlerCalled = true);

        lifetime.RequestShutdown();

        handlerCalled.Should().BeTrue();
    }
}
