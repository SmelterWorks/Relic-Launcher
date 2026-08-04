using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Transfers;

public sealed class TransferTracker : ITransferTracker
{
    private readonly Lock _gate = new();
    private readonly List<TransferJob> _jobs = [];
    private readonly int _maxConcurrent;
    private int _running;

    public TransferTracker(int maxConcurrent = 3)
    {
        _maxConcurrent = Math.Max(1, maxConcurrent);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<TransferJob> GetJobs()
    {
        lock (_gate)
        {
            return _jobs
                .OrderByDescending(j => j.StartedAt)
                .Select(Clone)
                .ToList();
        }
    }

    public ITransferSession Begin(string id, string label, TransferJobKind kind)
    {
        var job = new TransferJob
        {
            Id = id,
            Label = label,
            Kind = kind,
            State = TransferJobState.Queued,
            StatusText = "Queued",
            Progress = 0,
        };

        lock (_gate)
        {
            _jobs.RemoveAll(j =>
                string.Equals(j.Id, id, StringComparison.Ordinal) &&
                j.State is TransferJobState.Completed or TransferJobState.Failed or TransferJobState.Canceled);
            _jobs.Add(job);
            TrimLocked();
        }

        RaiseChanged();
        return new Session(this, job);
    }

    private async Task WaitForSlotAsync(TransferJob job, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (job.State == TransferJobState.Canceled)
                {
                    throw new OperationCanceledException();
                }

                if (_running < _maxConcurrent)
                {
                    _running++;
                    job.State = TransferJobState.Running;
                    job.StatusText = "Starting";
                    break;
                }
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        RaiseChanged();
    }

    private void Update(TransferJob job, Action<TransferJob> mutate)
    {
        lock (_gate)
        {
            mutate(job);
        }

        RaiseChanged();
    }

    private void Finish(TransferJob job, TransferJobState state, string? status, string? error)
    {
        lock (_gate)
        {
            if (job.State == TransferJobState.Running)
            {
                _running = Math.Max(0, _running - 1);
            }

            job.State = state;
            job.StatusText = status;
            job.Error = error;
            job.FinishedAt = DateTimeOffset.UtcNow;
            if (state == TransferJobState.Completed)
            {
                job.Progress = 1;
            }

            TrimLocked();
        }

        RaiseChanged();
    }

    private void TrimLocked()
    {
        var finished = _jobs
            .Where(j => j.State is TransferJobState.Completed or TransferJobState.Failed or TransferJobState.Canceled)
            .OrderByDescending(j => j.FinishedAt)
            .Skip(20)
            .ToList();
        foreach (var job in finished)
        {
            _jobs.Remove(job);
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static TransferJob Clone(TransferJob job)
        => new()
        {
            Id = job.Id,
            Label = job.Label,
            Kind = job.Kind,
            State = job.State,
            Progress = job.Progress,
            StatusText = job.StatusText,
            Error = job.Error,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt,
        };

    private sealed class Session : ITransferSession
    {
        private readonly TransferTracker _tracker;
        private readonly TransferJob _job;
        private int _started;

        public Session(TransferTracker tracker, TransferJob job)
        {
            _tracker = tracker;
            _job = job;
        }

        public string Id => _job.Id;

        public async ValueTask DisposeAsync()
        {
            if (_job.State is TransferJobState.Queued or TransferJobState.Running)
            {
                Cancel();
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public void Report(double value)
        {
            _tracker.Update(_job, j =>
            {
                j.Progress = Math.Clamp(value, 0, 1);
                if (j.State == TransferJobState.Running)
                {
                    j.StatusText = $"Downloading {j.Progress:P0}";
                }
            });
        }

        public void SetStatus(string status)
            => _tracker.Update(_job, j => j.StatusText = status);

        public Task StartAsync(CancellationToken cancellationToken = default)
            => EnsureStartedAsync(cancellationToken);

        private async Task EnsureStartedAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                return;
            }

            await _tracker.WaitForSlotAsync(_job, cancellationToken).ConfigureAwait(false);
        }

        public void Complete(string? status = null)
            => _tracker.Finish(_job, TransferJobState.Completed, status ?? "Done", null);

        public void Fail(string error)
            => _tracker.Finish(_job, TransferJobState.Failed, "Failed", error);

        public void Cancel()
            => _tracker.Finish(_job, TransferJobState.Canceled, "Canceled", null);
    }
}
