using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed partial class VersionRowViewModel : ViewModelBase
{
    private readonly VersionsViewModel _owner;

    public VersionRowViewModel(
        GameVersionInfo remote,
        InstalledGameVersion? installed,
        string? selectedVersion,
        VersionsViewModel owner)
    {
        _owner = owner;
        Version = remote.Version;
        Channel = remote.Channel.ToString();
        IsInstalled = installed is not null;
        IsActive = string.Equals(selectedVersion, remote.Version, StringComparison.OrdinalIgnoreCase);
        PackageSummary = string.Join(", ", remote.Packages.Select(p => p.PlatformKey));
        Remote = remote;
    }

    public string Version { get; }
    public string Channel { get; }
    public bool IsInstalled { get; }
    public bool IsActive { get; }
    public string PackageSummary { get; }
    public GameVersionInfo Remote { get; }

    [RelayCommand]
    private Task InstallAsync() => _owner.InstallAsync(Remote);

    [RelayCommand]
    private Task UninstallAsync() => _owner.UninstallAsync(Version);

    [RelayCommand]
    private Task SetActiveAsync() => _owner.SetActiveAsync(Version);
}
