using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed class LauncherUpdateModeOption
{
    public required LauncherUpdateMode Mode { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
}
