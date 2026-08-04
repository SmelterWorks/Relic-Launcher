using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Abstractions;

namespace RelicLauncher.App.Views;

public partial class CrashReportWindow : Window
{
    public CrashReportWindow()
    {
        InitializeComponent();
    }

    public static async Task ShowAsync(
        Window? owner,
        Exception exception,
        bool recovered,
        string? logsDirectory,
        IFileExplorerService? fileExplorer)
    {
        var report = CrashReportFormatter.Format(exception, recovered, logsDirectory);
        var vm = new CrashReportWindowViewModel(report, logsDirectory, fileExplorer);
        var window = new CrashReportWindow
        {
            DataContext = vm,
        };

        vm.RequestClose += (_, _) => window.Close();

        if (owner is not null)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (lifetime?.MainWindow is Window mainWindow && mainWindow != window)
        {
            await window.ShowDialog(mainWindow).ConfigureAwait(true);
            return;
        }

        window.Show();
        var tcs = new TaskCompletionSource();
        window.Closed += (_, _) => tcs.TrySetResult();
        await tcs.Task.ConfigureAwait(true);
    }
}
