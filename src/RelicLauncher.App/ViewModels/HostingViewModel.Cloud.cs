using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace RelicLauncher.App.ViewModels;

public partial class HostingViewModel
{
    [RelayCommand]
    private async Task LoadCloudPlansAsync()
    {
        if (IsLoadingCloudPlans)
        {
            return;
        }

        IsLoadingCloudPlans = true;
        OnPropertyChanged(nameof(ShowCloudPlanCards));
        try
        {
            var result = await _hostingFeed.GetPlansAsync().ConfigureAwait(true);
            CloudPlans.Clear();
            if (!result.IsSuccess)
            {
                SetStatus(result.Error ?? "Could not load SmelterWorks hosting plans.", true);
                return;
            }

            foreach (var plan in result.Value!)
            {
                CloudPlans.Add(new HostingPlanCardViewModel(plan));
            }

            SetStatus(string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load SmelterWorks hosting plans");
            SetStatus(ex.Message, true);
        }
        finally
        {
            IsLoadingCloudPlans = false;
            OnPropertyChanged(nameof(ShowCloudPlanCards));
            NotifyCloudPlansLayoutRefresh();
            Dispatcher.UIThread.Post(NotifyCloudPlansLayoutRefresh, DispatcherPriority.Render);
        }
    }

    partial void OnIsLoadingCloudPlansChanged(bool value) => OnPropertyChanged(nameof(ShowCloudPlanCards));

    [RelayCommand]
    private void OpenSmelterWorksHosting()
    {
        _urlLauncher.OpenUrl("https://smelterworks.com/hosting");
    }
}
