using Avalonia.Controls;
using Avalonia.Threading;
using RelicLauncher.App.Views;
using RelicLauncher.Core.Abstractions;

namespace RelicLauncher.App.Services;

internal static class CrashReportService
{
    private static int _showing;

    public static void TryShowFatal(Exception exception, string? logsDirectory, IFileExplorerService? fileExplorer)
    {
        if (Interlocked.CompareExchange(ref _showing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                _ = ShowOnUiThreadAsync(null, exception, recovered: false, logsDirectory, fileExplorer);
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                _ = ShowOnUiThreadAsync(null, exception, recovered: false, logsDirectory, fileExplorer);
            });
        }
        catch
        {
            Interlocked.Exchange(ref _showing, 0);
        }
    }

    public static Task ShowRecoveredAsync(
        Window owner,
        Exception exception,
        string? logsDirectory,
        IFileExplorerService? fileExplorer)
        => ShowOnUiThreadAsync(owner, exception, recovered: true, logsDirectory, fileExplorer);

    private static async Task ShowOnUiThreadAsync(
        Window? owner,
        Exception exception,
        bool recovered,
        string? logsDirectory,
        IFileExplorerService? fileExplorer)
    {
        try
        {
            await CrashReportWindow.ShowAsync(owner, exception, recovered, logsDirectory, fileExplorer)
                .ConfigureAwait(true);
        }
        finally
        {
            Interlocked.Exchange(ref _showing, 0);
        }
    }
}
