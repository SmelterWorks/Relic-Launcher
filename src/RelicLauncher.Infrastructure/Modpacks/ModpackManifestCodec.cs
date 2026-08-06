using System.Text.Json;
using RelicLauncher.Core.Models;

namespace RelicLauncher.Infrastructure.Modpacks;

internal static class ModpackManifestCodec
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static ModpackManifest Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<ModpackManifestDto>(json, ReadOptions)
                  ?? throw new JsonException("Manifest is empty.");
        return dto.ToManifest();
    }

    public static string Serialize(ModpackManifest manifest)
        => JsonSerializer.Serialize(ModpackManifestDto.From(manifest), WriteOptions);

    private sealed class ModpackManifestDto
    {
        public int SchemaVersion { get; set; } = 1;
        public string Format { get; set; } = "relic-modpack";
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GameVersion { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public string RelicVersion { get; set; } = string.Empty;
        public string Distribution { get; set; } = "online";
        public List<ModpackModEntryDto> Mods { get; set; } = [];

        public ModpackManifest ToManifest()
            => new()
            {
                SchemaVersion = SchemaVersion,
                Format = Format,
                Name = Name,
                Description = Description,
                GameVersion = GameVersion,
                CreatedAt = CreatedAt,
                RelicVersion = RelicVersion,
                Distribution = ParseDistribution(Distribution),
                Mods = Mods.Select(m => m.ToEntry()).ToList(),
            };

        public static ModpackManifestDto From(ModpackManifest manifest)
            => new()
            {
                SchemaVersion = manifest.SchemaVersion,
                Format = manifest.Format,
                Name = manifest.Name,
                Description = manifest.Description,
                GameVersion = manifest.GameVersion,
                CreatedAt = manifest.CreatedAt,
                RelicVersion = manifest.RelicVersion,
                Distribution = FormatDistribution(manifest.Distribution),
                Mods = manifest.Mods.Select(ModpackModEntryDto.From).ToList(),
            };
    }

    private sealed class ModpackModEntryDto
    {
        public string ModId { get; set; } = string.Empty;
        public string? ModVersion { get; set; }
        public int FileId { get; set; }
        public bool Enabled { get; set; } = true;
        public string Source { get; set; } = "moddb";
        public string? ArchivePath { get; set; }

        public ModpackModEntry ToEntry()
            => new()
            {
                ModId = ModId,
                ModVersion = ModVersion,
                FileId = FileId,
                Enabled = Enabled,
                Source = ParseSource(Source),
                ArchivePath = ArchivePath,
            };

        public static ModpackModEntryDto From(ModpackModEntry entry)
            => new()
            {
                ModId = entry.ModId,
                ModVersion = entry.ModVersion,
                FileId = entry.FileId,
                Enabled = entry.Enabled,
                Source = FormatSource(entry.Source),
                ArchivePath = entry.ArchivePath,
            };
    }

    private static ModpackDistribution ParseDistribution(string? value)
        => string.Equals(value, "offline", StringComparison.OrdinalIgnoreCase)
            ? ModpackDistribution.Offline
            : ModpackDistribution.Online;

    private static string FormatDistribution(ModpackDistribution distribution)
        => distribution == ModpackDistribution.Offline ? "offline" : "online";

    private static ModpackModSource ParseSource(string? value)
        => string.Equals(value, "local", StringComparison.OrdinalIgnoreCase)
            ? ModpackModSource.Local
            : ModpackModSource.ModDb;

    private static string FormatSource(ModpackModSource source)
        => source == ModpackModSource.Local ? "local" : "moddb";
}
