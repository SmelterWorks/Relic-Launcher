using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed partial class InstalledModRowViewModel : ViewModelBase
{
    private readonly IRemoteImageCache _images;
    private readonly IModLibraryService _modLibrary;
    private bool _logoLoadStarted;

    public InstalledModRowViewModel(
        LocalModInfo info,
        ModSummary? catalog,
        IRemoteImageCache images,
        IModLibraryService modLibrary,
        IReadOnlyList<ModDependencyIssue>? dependencyIssues = null)
    {
        Info = info;
        Catalog = catalog;
        _images = images;
        _modLibrary = modLibrary;
        Name = info.Name ?? info.FileName;
        Version = info.Version ?? string.Empty;
        FileName = info.FileName;
        IsEnabled = info.IsEnabled;
        Side = catalog?.Side ?? string.Empty;
        LogoUrl = catalog?.LogoUrl;
        Logo = ModIconAssets.Default;
        Tags = catalog?.Tags ?? [];
        TagsLabel = Tags.Count == 0
            ? string.Empty
            : string.Join(", ", Tags.Take(4));
        Downloads = catalog?.Downloads ?? 0;
        LastReleased = catalog?.LastReleased;
        ApplyDependencyIssues(dependencyIssues);
    }

    public LocalModInfo Info { get; }
    public ModSummary? Catalog { get; }
    public string Name { get; }
    public string Version { get; }
    public string FileName { get; }
    public bool IsEnabled { get; }
    public string Side { get; }
    public string? LogoUrl { get; }
    public IReadOnlyList<string> Tags { get; }
    public string TagsLabel { get; }
    public bool HasTags => !string.IsNullOrWhiteSpace(TagsLabel);
    public int Downloads { get; }
    public string? LastReleased { get; }
    public string DependencyStatusLabel { get; private set; } = string.Empty;
    public bool HasDependencyProblems { get; private set; }
    public bool ShowDependencyOk => !HasDependencyProblems && !string.IsNullOrWhiteSpace(DependencyStatusLabel);
    public bool HasUpdateAvailable { get; private set; }
    public string UpdateStatusLabel { get; private set; } = string.Empty;
    public bool WasRecentlyUpdated { get; private set; }
    public string RecentlyUpdatedLabel { get; private set; } = string.Empty;
    public ModUpdateCandidate? UpdateCandidate { get; private set; }

    [ObservableProperty]
    private Bitmap? _logo;

    [ObservableProperty]
    private bool _isLogoLoading;

    partial void OnLogoChanging(Bitmap? value)
        => OwnedBitmap.DisposeIfOwned(_logo);

    public void ApplyDependencyIssues(IReadOnlyList<ModDependencyIssue>? issues)
    {
        var blocking = issues?
            .Where(i => i.Kind != ModDependencyIssueKind.Satisfied)
            .ToList() ?? [];
        HasDependencyProblems = blocking.Count > 0;
        if (!HasDependencyProblems)
        {
            DependencyStatusLabel = Info.Dependencies.Count == 0
                ? string.Empty
                : "Dependencies ok";
            return;
        }

        var missing = blocking.Count(i => i.Kind == ModDependencyIssueKind.Missing);
        var disabled = blocking.Count(i => i.Kind == ModDependencyIssueKind.Disabled);
        var outdated = blocking.Count(i => i.Kind == ModDependencyIssueKind.Outdated);
        var parts = new List<string>();
        if (missing > 0)
        {
            parts.Add($"{missing} missing");
        }

        if (disabled > 0)
        {
            parts.Add($"{disabled} disabled");
        }

        if (outdated > 0)
        {
            parts.Add($"{outdated} outdated");
        }

        if (parts.Count == 0)
        {
            parts.Add($"{blocking.Count} dependency issue(s)");
        }

        DependencyStatusLabel = string.Join(", ", parts);
    }

    public void ApplyUpdateState(ModUpdateCandidate? candidate, bool wasRecentlyUpdated, string? recentlyUpdatedVersion)
    {
        UpdateCandidate = candidate;
        HasUpdateAvailable = candidate is not null;
        UpdateStatusLabel = candidate is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(candidate.AvailableVersion)
                ? "Update available"
                : $"Update available ({candidate.AvailableVersion})";
        WasRecentlyUpdated = wasRecentlyUpdated;
        RecentlyUpdatedLabel = wasRecentlyUpdated
            ? string.IsNullOrWhiteSpace(recentlyUpdatedVersion)
                ? "Updated"
                : $"Updated ({recentlyUpdatedVersion})"
            : string.Empty;
        OnPropertyChanged(nameof(HasUpdateAvailable));
        OnPropertyChanged(nameof(UpdateStatusLabel));
        OnPropertyChanged(nameof(WasRecentlyUpdated));
        OnPropertyChanged(nameof(RecentlyUpdatedLabel));
        OnPropertyChanged(nameof(UpdateCandidate));
    }

    public void ClearRecentlyUpdatedIndicator()
    {
        if (!WasRecentlyUpdated)
        {
            return;
        }

        WasRecentlyUpdated = false;
        RecentlyUpdatedLabel = string.Empty;
        OnPropertyChanged(nameof(WasRecentlyUpdated));
        OnPropertyChanged(nameof(RecentlyUpdatedLabel));
    }

    public void UnloadLogo()
    {
        _logoLoadStarted = false;
        Logo = ModIconAssets.Default;
    }

    public async Task LoadLogoAsync()
    {
        if (_logoLoadStarted)
        {
            return;
        }

        _logoLoadStarted = true;
        IsLogoLoading = true;
        try
        {
            var localBytes = _modLibrary.TryReadModIcon(Info);
            if (localBytes is { Length: > 0 })
            {
                Logo = ScaledBitmapLoader.FromBytes(localBytes, RelicDefaults.DecodeWidthModListLogo)
                       ?? ModIconAssets.Default;
                return;
            }

            if (string.IsNullOrWhiteSpace(LogoUrl))
            {
                Logo = ModIconAssets.Default;
                return;
            }

            var bytes = await _images.GetImageBytesAsync(LogoUrl).ConfigureAwait(true);
            if (bytes is null)
            {
                Logo = ModIconAssets.Default;
                return;
            }

            Logo = ScaledBitmapLoader.FromBytes(bytes, RelicDefaults.DecodeWidthModListLogo)
                   ?? ModIconAssets.Default;
        }
        finally
        {
            IsLogoLoading = false;
        }
    }
}
