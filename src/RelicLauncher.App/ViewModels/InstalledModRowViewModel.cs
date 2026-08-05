using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed partial class InstalledModRowViewModel : ViewModelBase
{
    private readonly IRemoteImageCache _images;
    private readonly IModLibraryService _modLibrary;

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

    [ObservableProperty]
    private Bitmap? _logo;

    [ObservableProperty]
    private bool _isLogoLoading;

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

    public async Task LoadLogoAsync()
    {
        IsLogoLoading = true;
        try
        {
            var localBytes = _modLibrary.TryReadModIcon(Info);
            if (localBytes is { Length: > 0 })
            {
                using var localStream = new MemoryStream(localBytes);
                Logo = new Bitmap(localStream);
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

            using var stream = new MemoryStream(bytes);
            Logo = new Bitmap(stream);
        }
        finally
        {
            IsLogoLoading = false;
        }
    }
}
