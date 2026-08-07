using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Abstractions;

namespace RelicLauncher.App.ViewModels;

public partial class CrashReportWindowViewModel : ObservableObject
{
    private readonly string _reportText;
    private readonly string? _logsDirectory;
    private readonly IFileExplorerService? _fileExplorer;

    public CrashReportWindowViewModel(string reportText, string? logsDirectory, IFileExplorerService? fileExplorer)
    {
        _reportText = reportText;
        _logsDirectory = logsDirectory;
        _fileExplorer = fileExplorer;
        Heading = "Relic Launcher hit an error";
        Subheading = string.IsNullOrWhiteSpace(logsDirectory)
            ? "Copy this report or check the application logs folder."
            : $"Copy this report or open the logs folder:\n{logsDirectory}";
        ReportText = reportText;
    }

    public string Heading { get; }

    public string Subheading { get; }

    public string ReportText { get; }

    [ObservableProperty]
    private string _copyStatusMessage = string.Empty;

    public event EventHandler? RequestClose;

    [RelayCommand]
    private async Task CopyReportAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var clipboard = desktop.MainWindow?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(_reportText).ConfigureAwait(true);
        CopyStatusMessage = "Copied to clipboard.";
    }

    [RelayCommand]
    private void OpenLogs()
    {
        if (string.IsNullOrWhiteSpace(_logsDirectory) || _fileExplorer is null)
        {
            return;
        }

        _fileExplorer.OpenFolder(_logsDirectory);
    }

    [RelayCommand]
    private void Close()
        => RequestClose?.Invoke(this, EventArgs.Empty);
}
