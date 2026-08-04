namespace RelicLauncher.Core.Abstractions;

public interface ITransferSession : IAsyncDisposable, IProgress<double>
{
    string Id { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    void SetStatus(string status);

    void Complete(string? status = null);

    void Fail(string error);

    void Cancel();
}
