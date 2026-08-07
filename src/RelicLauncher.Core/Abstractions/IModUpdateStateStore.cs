namespace RelicLauncher.Core.Abstractions;

public interface IModUpdateStateStore
{
    DateTimeOffset? GetLastCheckUtc();

    void SetLastCheckUtc(DateTimeOffset value);

    IReadOnlyDictionary<string, string> GetRecentlyUpdatedMods();

    void MarkRecentlyUpdated(string modId, string version);

    void ClearRecentlyUpdated(string modId);

    void ClearAllRecentlyUpdated();
}
