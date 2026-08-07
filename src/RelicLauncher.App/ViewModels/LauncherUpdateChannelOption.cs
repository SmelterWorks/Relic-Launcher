using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed class LauncherUpdateChannelOption
{
    public required LauncherUpdateChannel Channel { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
}
