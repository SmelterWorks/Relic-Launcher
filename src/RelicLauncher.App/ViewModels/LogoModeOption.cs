using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed class LogoModeOption
{
    public required HomeBackgroundLogoMode Mode { get; init; }
    public required string Label { get; init; }
}
