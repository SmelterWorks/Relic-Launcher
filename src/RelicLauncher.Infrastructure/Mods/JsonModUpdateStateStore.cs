using System.Text.Json;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Mods;

public sealed class JsonModUpdateStateStore : IModUpdateStateStore
{
    private const string StateFileName = "update-state.json";
    private readonly IAppPathProvider _pathProvider;
    private readonly Lock _gate = new();
    private StateDto? _state;

    public JsonModUpdateStateStore(IAppPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public DateTimeOffset? GetLastCheckUtc()
    {
        lock (_gate)
        {
            return EnsureLoaded().LastCheckUtc;
        }
    }

    public void SetLastCheckUtc(DateTimeOffset value)
    {
        lock (_gate)
        {
            var state = EnsureLoaded();
            state.LastCheckUtc = value;
            Save(state);
        }
    }

    public IReadOnlyDictionary<string, string> GetRecentlyUpdatedMods()
    {
        lock (_gate)
        {
            return new Dictionary<string, string>(EnsureLoaded().RecentlyUpdated, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void MarkRecentlyUpdated(string modId, string version)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return;
        }

        lock (_gate)
        {
            var state = EnsureLoaded();
            state.RecentlyUpdated[modId.Trim()] = version.Trim();
            Save(state);
        }
    }

    public void ClearRecentlyUpdated(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return;
        }

        lock (_gate)
        {
            var state = EnsureLoaded();
            state.RecentlyUpdated.Remove(modId.Trim());
            Save(state);
        }
    }

    public void ClearAllRecentlyUpdated()
    {
        lock (_gate)
        {
            var state = EnsureLoaded();
            state.RecentlyUpdated.Clear();
            Save(state);
        }
    }

    private StateDto EnsureLoaded()
    {
        if (_state is not null)
        {
            return _state;
        }

        var path = GetStatePath();
        if (!File.Exists(path))
        {
            _state = new StateDto();
            return _state;
        }

        try
        {
            var json = File.ReadAllText(path);
            _state = JsonSerializer.Deserialize<StateDto>(json, JsonOptions) ?? new StateDto();
        }
        catch (JsonException)
        {
            _state = new StateDto();
        }

        return _state;
    }

    private void Save(StateDto state)
    {
        var path = GetStatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }

    private string GetStatePath()
        => Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", StateFileName);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed class StateDto
    {
        public DateTimeOffset? LastCheckUtc { get; set; }
        public Dictionary<string, string> RecentlyUpdated { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
