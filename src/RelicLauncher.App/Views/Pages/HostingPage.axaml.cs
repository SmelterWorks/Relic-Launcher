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
    }

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
        => Dispatcher.UIThread.Post(RefreshCloudPlansLayout, DispatcherPriority.Loaded);

    private void RefreshCloudPlansLayout()
    {
        CloudPlansPanel?.InvalidateMeasure();
        CloudPlansItems?.InvalidateMeasure();
        CloudPlanScroll?.InvalidateMeasure();
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
