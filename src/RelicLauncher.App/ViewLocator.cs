using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RelicLauncher.App.ViewModels;
using RelicLauncher.App.Views.Pages;

namespace RelicLauncher.App;

public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        return param switch
        {
            HomeViewModel => new HomePage(),
            VersionsViewModel => new VersionsPage(),
            ModsViewModel => new ModsPage(),
            BackupViewModel => new BackupPage(),
            WikiViewModel => new WikiPage(),
            SettingsViewModel => new SettingsPage(),
            AboutViewModel => new AboutPage(),
            _ => new TextBlock { Text = $"No view for {param?.GetType().Name}" },
        };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
