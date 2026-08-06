using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class ModpackRowViewModel : ObservableObject
{
    public ModpackLocalSummary Summary { get; }
    public ModpackRowViewModel(ModpackLocalSummary summary)
    {
        Summary = summary;
    }

    public string Name => Summary.Name;
    public string GameVersion => Summary.GameVersion;
    public int ModCount => Summary.ModCount;
    public string DistributionLabel => Summary.Distribution == ModpackDistribution.Offline ? "Offline" : "Online";
    public string Description => Summary.Description;
}
