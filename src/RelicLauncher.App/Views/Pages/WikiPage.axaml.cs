using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views.Pages;

public partial class WikiPage : UserControl
{
    private Panel? _webViewHost;
    private NativeWebView? _webView;
    private WikiViewModel? _boundVm;
    private TopLevel? _topLevel;
    private bool _suppressNavigationHandler;

    public WikiPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _webViewHost = this.FindControl<Panel>("WikiWebViewHost");
        RegisterScrollWheelCapture();
        HookViewModel(DataContext as WikiViewModel);
        TryEnsureWebView();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UnregisterScrollWheelCapture();
        DestroyWebView();
        UnhookViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
        => HookViewModel(DataContext as WikiViewModel);

    private void HookViewModel(WikiViewModel? vm)
    {
        if (ReferenceEquals(_boundVm, vm))
        {
            return;
        }

        UnhookViewModel();
        _boundVm = vm;
        if (vm is null)
        {
            return;
        }

        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.NavigateRequested = NavigateTo;
        vm.ReloadRequested = Reload;
        vm.GoBackRequested = GoBack;
        vm.GoForwardRequested = GoForward;
        vm.ClearSiteDataRequested = ClearSiteData;
        vm.NotifyHostReady();
        TryEnsureWebView();
    }

    private void UnhookViewModel()
    {
        if (_boundVm is null)
        {
            return;
        }

        _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
        _boundVm.NavigateRequested = null;
        _boundVm.ReloadRequested = null;
        _boundVm.GoBackRequested = null;
        _boundVm.GoForwardRequested = null;
        _boundVm.ClearSiteDataRequested = null;
        _boundVm = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WikiViewModel.ShowWebView) or nameof(WikiViewModel.IsWebViewAvailable))
        {
            if (_boundVm?.ShowWebView == true && _boundVm.IsWebViewAvailable)
            {
                TryEnsureWebView();
            }
            else
            {
                DestroyWebView();
            }
        }
    }
}
