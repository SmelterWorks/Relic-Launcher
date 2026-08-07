using CommunityToolkit.Mvvm.Input;

namespace RelicLauncher.App.ViewModels;

public partial class HostingViewModel
{
    [RelayCommand]
    private async Task SendCommandAsync()
    {
        if (!CanSendCommand || string.IsNullOrWhiteSpace(CommandText))
        {
            return;
        }

        var command = CommandText.Trim();
        CommandText = string.Empty;
        var result = await _serverHost.SendCommandAsync(command).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            SetStatus(result.Error ?? "Could not send command.", true);
        }
    }

    [RelayCommand]
    private void ClearConsole()
    {
        _serverHost.ClearOutput();
        RefreshConsoleText(_serverHost.OutputLines);
    }
}
