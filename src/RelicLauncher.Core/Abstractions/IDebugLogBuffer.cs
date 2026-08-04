using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface IDebugLogBuffer
{
    IReadOnlyList<DebugLogEntry> GetEntries();

    event EventHandler? Changed;

    void Clear();
}
