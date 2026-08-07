using CommunityToolkit.Mvvm.ComponentModel;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public partial class SettingsViewModel
{
    [ObservableProperty]
    private bool _processIsolationEnabled = true;

    [ObservableProperty]
    private string _isolationStatus = string.Empty;

    private void BindSandbox(LauncherSettings settings)
    {
        ProcessIsolationEnabled = settings.ProcessIsolationEnabled;
        IsolationStatus = _sandboxSupport.GetStatusSummary();
    }

    partial void OnProcessIsolationEnabledChanged(bool value) => ScheduleAutoSave();

    private void ApplySandboxToSettings(LauncherSettings settings)
    {
        settings.ProcessIsolationEnabled = ProcessIsolationEnabled;
    }
}
