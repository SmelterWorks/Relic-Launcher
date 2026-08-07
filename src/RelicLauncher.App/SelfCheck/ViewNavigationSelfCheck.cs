using RelicLauncher.App.ViewModels;
using RelicLauncher.App.Views.Pages;
using RelicLauncher.Core.SelfCheck;

namespace RelicLauncher.App.SelfCheck;

internal static class ViewNavigationSelfCheck
{
    private static readonly (Type ViewModelType, Type PageType)[] Routes =
    [
        (typeof(HomeViewModel), typeof(HomePage)),
        (typeof(VersionsViewModel), typeof(VersionsPage)),
        (typeof(ModsViewModel), typeof(ModsPage)),
        (typeof(BackupViewModel), typeof(BackupPage)),
        (typeof(ServersViewModel), typeof(ServersPage)),
        (typeof(HostingViewModel), typeof(HostingPage)),
        (typeof(WikiViewModel), typeof(WikiPage)),
        (typeof(SettingsViewModel), typeof(SettingsPage)),
        (typeof(AboutViewModel), typeof(AboutPage)),
    ];

    public static SelfCheckItem Verify()
    {
        var failures = new List<string>();

        foreach (var (viewModelType, pageType) in Routes)
        {
            if (!typeof(ViewModelBase).IsAssignableFrom(viewModelType))
            {
                failures.Add($"{viewModelType.Name} is not a ViewModelBase");
            }

            if (pageType.GetConstructor(Type.EmptyTypes) is null)
            {
                failures.Add($"{pageType.Name} is missing a parameterless constructor");
            }
        }

        if (typeof(ViewLocator).GetMethod(nameof(ViewLocator.Build), [typeof(object)]) is null)
        {
            failures.Add("ViewLocator.Build is missing");
        }

        if (failures.Count > 0)
        {
            return SelfCheckItem.Fail("navigation", "Page navigation", string.Join("; ", failures));
        }

        return SelfCheckItem.Pass("navigation", "Page navigation", $"{Routes.Length} routes");
    }
}
