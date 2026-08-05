namespace RelicLauncher.Core.Mods;

public static class BuiltinModIds
{
    public const string Game = "game";
    public const string Survival = "survival";
    public const string Creative = "creative";

    private static readonly HashSet<string> Ids = new(StringComparer.OrdinalIgnoreCase)
    {
        Game,
        Survival,
        Creative,
    };

    public static bool IsBuiltin(string? modId)
        => !string.IsNullOrWhiteSpace(modId) && Ids.Contains(modId.Trim());
}
