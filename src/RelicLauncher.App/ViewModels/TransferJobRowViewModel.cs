using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed class TransferJobRowViewModel
{
    public TransferJobRowViewModel(TransferJob job)
    {
        Id = job.Id;
        Label = job.Label;
        StatusText = job.StatusText ?? job.State.ToString();
        Progress = job.Progress;
        IsActive = job.State is TransferJobState.Queued or TransferJobState.Running;
        Kind = job.Kind.ToString();
    }

    public string Id { get; }
    public string Label { get; }
    public string StatusText { get; }
    public double Progress { get; }
    public bool IsActive { get; }
    public string Kind { get; }
}
