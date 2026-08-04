using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface ITransferTracker
{
    IReadOnlyList<TransferJob> GetJobs();

    event EventHandler? Changed;

    ITransferSession Begin(string id, string label, TransferJobKind kind);
}
