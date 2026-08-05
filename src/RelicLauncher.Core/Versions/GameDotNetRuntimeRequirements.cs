using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Versions;

public static class GameDotNetRuntimeRequirements
{
    private const string MinSupportedGameVersion = "1.18.8";
    private const string Net8Floor = "1.21.0";
    private const string Net10Floor = "1.22.0";

    public static Result<int> TryGetRequiredMajor(string? gameVersion)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return Result<int>.Failure("Game version is empty.");
        }

        var trimmed = gameVersion.Trim();
        if (GameVersionComparer.Compare(trimmed, MinSupportedGameVersion) < 0)
        {
            return Result<int>.Failure(
                $"Game version {trimmed} needs .NET Framework 4 / Mono, which Relic does not provision.");
        }

        if (GameVersionComparer.Compare(trimmed, Net8Floor) < 0)
        {
            return Result<int>.Success(7);
        }

        if (GameVersionComparer.Compare(trimmed, Net10Floor) < 0)
        {
            return Result<int>.Success(8);
        }

        return Result<int>.Success(10);
    }
}
