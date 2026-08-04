using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Wiki;

namespace RelicLauncher.App.ViewModels;

public partial class WikiViewModel : PageViewModelBase
{
    private readonly IEndpointProvider _endpoints;
    private readonly IUrlLauncher _urlLauncher;
    private readonly IWikiReachabilityProbe _probe;
    private CancellationTokenSource? _probeCts;
    private string _boundWikiBase = string.Empty;
    private bool _initialized;
    private bool _hostReady;
    private bool _pendingActivate;

    [ObservableProperty]
    private string _currentUrl = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    private bool _isWebViewAvailable = true;

    [ObservableProperty]
    private bool _showWebView = true;

    public WikiViewModel(
        IEndpointProvider endpoints,
        IUrlLauncher urlLauncher,
        IWikiReachabilityProbe probe)
    {
        _endpoints = endpoints;
        _urlLauncher = urlLauncher;
        _probe = probe;
    }

    public string WikiBaseUrl => _endpoints.WikiBaseUrl;

    public Action<Uri>? NavigateRequested { get; set; }
    public Action? ReloadRequested { get; set; }
    public Func<bool>? GoBackRequested { get; set; }
    public Func<bool>? GoForwardRequested { get; set; }
    public Action? ClearSiteDataRequested { get; set; }

    public void NotifyHostReady()
    {
        _hostReady = true;
        if (_pendingActivate || !_initialized)
        {
            _pendingActivate = false;
            _initialized = true;
            _ = ActivateAsync(navigateHome: true);
        }
    }

    public void Bind(LauncherSettings settings, bool refresh = true)
    {
        _ = settings;
        var baseUrl = _endpoints.WikiBaseUrl;
        var hostChanged = !string.Equals(_boundWikiBase, baseUrl, StringComparison.OrdinalIgnoreCase);

        if (hostChanged && _initialized && _hostReady)
        {
            ClearSiteDataRequested?.Invoke();
        }

        _boundWikiBase = baseUrl;

        if (!_initialized || refresh || hostChanged)
        {
            if (_hostReady)
            {
                _initialized = true;
                _pendingActivate = false;
                _ = ActivateAsync(navigateHome: true);
            }
            else
            {
                _pendingActivate = true;
            }
        }
    }

    public WikiNavigationDecision EvaluateNavigation(string? url, out Uri? resolved)
        => WikiNavigationGuard.Evaluate(WikiBaseUrl, url, out resolved);

    public void HandleAllowedNavigationStarted(Uri url)
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        CurrentUrl = url.AbsoluteUri;
        ShowWebView = true;
    }

    public void HandleExternalNavigation(Uri url)
    {
        _urlLauncher.OpenUrl(url.AbsoluteUri);
    }

    public void HandleNavigationCompleted(Uri? url, bool success)
    {
        IsLoading = false;
        if (url is not null)
        {
            CurrentUrl = url.AbsoluteUri;
        }

        if (!success && !HasError)
        {
            ShowError(
                "The wiki page failed to load.",
                keepWebViewVisible: false);
        }
    }

    public void UpdateHistoryState(bool canGoBack, bool canGoForward)
    {
        CanGoBack = canGoBack;
        CanGoForward = canGoForward;
    }

    public void ReportWebViewUnavailable(string? detail = null)
    {
        IsWebViewAvailable = false;
        IsLoading = false;
        ShowError(
            detail ?? "Embedded browser is unavailable on this system. Install WebView2 (Windows) or WPE WebKit (Linux), or open the wiki in your browser.",
            keepWebViewVisible: false);
    }

    [RelayCommand]
    private void GoHome()
    {
        if (!WikiNavigationGuard.TryParseAbsoluteBase(WikiBaseUrl, out var home))
        {
            ShowError("Wiki URL in Settings is not a valid http(s) address.", keepWebViewVisible: false);
            return;
        }

        HasError = false;
        ErrorMessage = string.Empty;
        ShowWebView = IsWebViewAvailable;
        NavigateRequested?.Invoke(home);
    }

    [RelayCommand(CanExecute = nameof(CanReload))]
    private void Reload()
    {
        if (HasError || !IsWebViewAvailable)
        {
            _ = ActivateAsync(navigateHome: true);
            return;
        }

        ReloadRequested?.Invoke();
    }

    private bool CanReload() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private void NavigateBack()
    {
        if (GoBackRequested?.Invoke() == true)
        {
            IsLoading = true;
        }
    }

    private bool CanNavigateBack() => CanGoBack && !IsLoading;

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    private void NavigateForward()
    {
        if (GoForwardRequested?.Invoke() == true)
        {
            IsLoading = true;
        }
    }

    private bool CanNavigateForward() => CanGoForward && !IsLoading;

    [RelayCommand]
    private void OpenInBrowser()
    {
        var target = string.IsNullOrWhiteSpace(CurrentUrl) ? WikiBaseUrl : CurrentUrl;
        var decision = WikiNavigationGuard.Evaluate(WikiBaseUrl, target, out var resolved);
        if (decision == WikiNavigationDecision.Allow && resolved is not null)
        {
            _urlLauncher.OpenUrl(resolved.AbsoluteUri);
            return;
        }

        if (WikiNavigationGuard.TryParseAbsoluteBase(WikiBaseUrl, out var home))
        {
            _urlLauncher.OpenUrl(home.AbsoluteUri);
        }
    }

    [RelayCommand]
    private void Retry() => _ = ActivateAsync(navigateHome: HasError || string.IsNullOrWhiteSpace(CurrentUrl));

    partial void OnIsLoadingChanged(bool value)
    {
        ReloadCommand.NotifyCanExecuteChanged();
        NavigateBackCommand.NotifyCanExecuteChanged();
        NavigateForwardCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanGoBackChanged(bool value) => NavigateBackCommand.NotifyCanExecuteChanged();

    partial void OnCanGoForwardChanged(bool value) => NavigateForwardCommand.NotifyCanExecuteChanged();

    private async Task ActivateAsync(bool navigateHome)
    {
        _probeCts?.Cancel();
        _probeCts?.Dispose();
        _probeCts = new CancellationTokenSource();
        var token = _probeCts.Token;

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        if (!WikiNavigationGuard.TryParseAbsoluteBase(WikiBaseUrl, out var home))
        {
            ShowError("Wiki URL in Settings is not a valid http(s) address.", keepWebViewVisible: false);
            return;
        }

        var probe = await _probe.ProbeAsync(token).ConfigureAwait(true);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (!probe.IsSuccess || probe.Value is null)
        {
            ShowError(probe.Error ?? "Could not check wiki availability.", keepWebViewVisible: false);
            return;
        }

        var result = probe.Value;
        if (!result.IsReachable)
        {
            ShowError(DescribeProbeFailure(result), keepWebViewVisible: false);
            if (WikiNavigationGuard.TryParseAbsoluteBase(WikiBaseUrl, out _))
            {
                CurrentUrl = home.AbsoluteUri;
            }

            return;
        }

        ShowWebView = IsWebViewAvailable;
        if (navigateHome || string.IsNullOrWhiteSpace(CurrentUrl))
        {
            CurrentUrl = home.AbsoluteUri;
            NavigateRequested?.Invoke(home);
        }
        else if (HasError)
        {
            ReloadRequested?.Invoke();
        }
        else
        {
            IsLoading = false;
        }
    }

    private static string DescribeProbeFailure(WikiReachabilityResult result)
        => result.Status switch
        {
            WikiReachabilityStatus.NetworkFailure =>
                "Wiki is unreachable. Check your network, or open it in your browser.",
            WikiReachabilityStatus.TemporarilyUnavailable =>
                "Wiki is temporarily unavailable (rate limit or overload). Try again later, or open it in your browser.",
            WikiReachabilityStatus.AccessBlocked =>
                "Wiki may be blocking embedded access (WAF or challenge page). Open it in your browser.",
            WikiReachabilityStatus.ServerError =>
                "Wiki returned a server error. Try again, or open it in your browser.",
            _ => result.Detail ?? "Wiki is unavailable.",
        };

    private void ShowError(string message, bool keepWebViewVisible)
    {
        IsLoading = false;
        HasError = true;
        ErrorMessage = message;
        StatusMessage = message;
        ShowWebView = keepWebViewVisible && IsWebViewAvailable;
    }
}
