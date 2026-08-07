using CommunityToolkit.Mvvm.ComponentModel;

namespace RelicLauncher.App.ViewModels;

public partial class ModsViewModel
{
    [ObservableProperty]
    private ModRowViewModel? _selectedBrowseMod;

    [ObservableProperty]
    private InstalledModRowViewModel? _selectedInstalledMod;

    partial void OnSelectedBrowseModChanged(ModRowViewModel? value)
    {
        if (value is not null)
        {
            _ = OpenModAsync(value.Summary);
        }
    }

    partial void OnSelectedInstalledModChanged(InstalledModRowViewModel? value)
    {
        if (value is not null)
        {
            _ = OpenInstalledModAsync(value);
        }
    }
}
