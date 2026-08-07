using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed class ModUpdateModeOption
{
    public required ModUpdateMode Mode { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
}
