using RelicLauncher.Core.Models;

namespace RelicLauncher.Core.Abstractions;

public interface IModOriginResolver
{
    ModOriginInfo Resolve(LocalModInfo mod);

    IReadOnlyList<ModFileIndexEntry> GetIndexEntries();
}
