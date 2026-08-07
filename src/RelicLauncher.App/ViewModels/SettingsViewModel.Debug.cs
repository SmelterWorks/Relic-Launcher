using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class SettingsViewModel
{
    [RelayCommand]
    private void RefreshDebugLog()
    {
        var entries = _debugLogBuffer.GetEntries();
        if (entries.Count == 0)
        {
            DebugLogText = "No warnings or errors captured yet.";
            return;
        }

        var sb = new StringBuilder();
        foreach (var entry in entries.Take(120))
        {
            sb.Append(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.Append(" [").Append(entry.Level).Append("] ");
            if (!string.IsNullOrWhiteSpace(entry.Source))
            {
                sb.Append(entry.Source).Append(": ");
            }

            sb.AppendLine(entry.Message);
            if (!string.IsNullOrWhiteSpace(entry.Exception))
            {
                sb.AppendLine(entry.Exception);
            }

            sb.AppendLine();
        }

        DebugLogText = sb.ToString();
    }

    [RelayCommand]
    private void ClearDebugLog()
    {
        _debugLogBuffer.Clear();
        RefreshDebugLog();
    }

    [RelayCommand]
    private void ToggleDebugViewer() => ShowDebugViewer = !ShowDebugViewer;

    private void OnDebugLogChanged()
    {
        if (!ShowDebugViewer)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshDebugLog();
        }
        else
        {
            Dispatcher.UIThread.Post(RefreshDebugLog);
        }
    }
}
