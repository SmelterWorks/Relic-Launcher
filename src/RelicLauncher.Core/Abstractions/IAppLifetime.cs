namespace RelicLauncher.Core.Abstractions;

public interface IAppLifetime
{
    CancellationToken ApplicationStopping { get; }
    void RequestShutdown();
}
