using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views.Pages;

public partial class HostingPage : UserControl
{
    public HostingPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        LayoutUpdated += OnLayoutUpdated;
    }

    private bool _cloudPlansLayoutPending;

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        HookCloudPlansLayoutRefresh();
        ScheduleCloudPlansLoadAndLayoutRefresh();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is HostingViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            HookCloudPlansLayoutRefresh();
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_cloudPlansLayoutPending || DataContext is not HostingViewModel vm || !vm.ShowCloudPlanCards)
        {
            return;
        }

        if (CloudPlanScroll?.Bounds.Width > 0)
        {
            _cloudPlansLayoutPending = false;
            RefreshCloudPlansLayout();
        }
    }

    private void HookCloudPlansLayoutRefresh()
    {
        if (DataContext is not HostingViewModel vm)
        {
            return;
        }

        vm.CloudPlansLayoutRefreshRequested -= OnCloudPlansLayoutRefreshRequested;
        vm.CloudPlansLayoutRefreshRequested += OnCloudPlansLayoutRefreshRequested;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(HostingViewModel.ConsoleText), StringComparison.Ordinal))
        {
            ConsoleScroll?.ScrollToEnd();
            return;
        }

        if (string.Equals(e.PropertyName, nameof(HostingViewModel.IsCloudSection), StringComparison.Ordinal)
            && DataContext is HostingViewModel vm
            && vm.IsCloudSection)
        {
            ScheduleCloudPlansLoadAndLayoutRefresh();
            return;
        }

        if (string.Equals(e.PropertyName, nameof(HostingViewModel.ShowCloudPlanCards), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(HostingViewModel.IsLoadingCloudPlans), StringComparison.Ordinal))
        {
            ScheduleCloudPlansLayoutRefresh();
        }
    }

    private void OnCloudPlansLayoutRefreshRequested(object? sender, EventArgs e)
        => ScheduleCloudPlansLayoutRefresh();

    private void ScheduleCloudPlansLoadAndLayoutRefresh()
    {
        if (DataContext is HostingViewModel vm)
        {
            vm.RequestCloudPlansLoadIfNeeded();
        }

        ScheduleCloudPlansLayoutRefresh();
    }

    private void ScheduleCloudPlansLayoutRefresh()
    {
        _cloudPlansLayoutPending = true;
        Dispatcher.UIThread.Post(RefreshCloudPlansLayout, DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(RefreshCloudPlansLayout, DispatcherPriority.Render);
    }

    private void RefreshCloudPlansLayout()
    {
        if (CloudPlanScroll is not null && CloudPlansItems is not null)
        {
            var width = CloudPlanScroll.Bounds.Width;
            if (width > 0)
            {
                CloudPlansItems.Width = width;
            }
        }

        CloudPlansPanel?.InvalidateMeasure();
        CloudPlansPanel?.InvalidateArrange();
        CloudPlansItems?.InvalidateMeasure();
        CloudPlansItems?.InvalidateArrange();
        CloudPlanScroll?.InvalidateMeasure();
        CloudPlanScroll?.InvalidateArrange();
    }

    private void OnCommandKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not HostingViewModel vm || !vm.CanSendCommand)
        {
            return;
        }

        if (vm.SendCommandCommand.CanExecute(null))
        {
            vm.SendCommandCommand.Execute(null);
            e.Handled = true;
        }
    }
}
