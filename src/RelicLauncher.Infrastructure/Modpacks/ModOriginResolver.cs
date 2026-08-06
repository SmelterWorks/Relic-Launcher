using System.Text.Json;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;

namespace RelicLauncher.Infrastructure.Modpacks;

public sealed class ModOriginResolver : IModOriginResolver
{
    private const string IndexFileName = "index.json";
    private readonly IAppPathProvider _pathProvider;
    private readonly Lazy<IReadOnlyList<ModFileIndexEntry>> _index;

    public ModOriginResolver(IAppPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
        _index = new Lazy<IReadOnlyList<ModFileIndexEntry>>(LoadIndex);
    }

    public ModOriginInfo Resolve(LocalModInfo mod)
        => ModOriginClassifier.Classify(mod, _index.Value);

    public IReadOnlyList<ModFileIndexEntry> GetIndexEntries()
        => _index.Value;

    private IReadOnlyList<ModFileIndexEntry> LoadIndex()
    {
        var cacheDir = Path.Combine(_pathProvider.GetPaths().CacheDirectory, "mods", "files");
        var indexPath = Path.Combine(cacheDir, IndexFileName);
        if (!File.Exists(indexPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(indexPath);
            var map = JsonSerializer.Deserialize<Dictionary<string, ModFileIndexEntryDto>>(json, JsonOptions)
                      ?? new Dictionary<string, ModFileIndexEntryDto>(StringComparer.Ordinal);
            return map.Values
                .Select(v => new ModFileIndexEntry
                {
                    FileId = v.FileId,
                    FileName = v.FileName,
                    ModId = v.ModId,
                })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class ModFileIndexEntryDto
    {
        public int FileId { get; set; }
        public string? FileName { get; set; }
        public string? ModId { get; set; }
    }
}
