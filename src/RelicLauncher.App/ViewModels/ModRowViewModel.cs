using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed partial class ModRowViewModel : ViewModelBase
{
    private readonly IRemoteImageCache _images;
    private readonly Func<ModSummary?, Task> _open;
    private bool _logoLoadStarted;

    public ModRowViewModel(ModSummary summary, IRemoteImageCache images, Func<ModSummary?, Task> open)
    {
        Summary = summary;
        _images = images;
        _open = open;
        Name = summary.Name;
        Author = summary.Author ?? string.Empty;
        SummaryText = summary.Summary ?? string.Empty;
        DownloadsLabel = $"{summary.Downloads:N0} downloads";
        LogoUrl = summary.LogoUrl;
        Logo = ModIconAssets.Default;
        TagsLabel = summary.Tags.Count == 0
            ? string.Empty
            : string.Join(", ", summary.Tags.Take(4));
    }

    public ModSummary Summary { get; }
    public string Name { get; }
    public string Author { get; }
    public string SummaryText { get; }
    public string DownloadsLabel { get; }
    public string TagsLabel { get; }
    public string? LogoUrl { get; }
    public bool HasTags => !string.IsNullOrWhiteSpace(TagsLabel);

    [ObservableProperty]
    private Bitmap? _logo;

    [ObservableProperty]
    private bool _isLogoLoading;

    [RelayCommand]
    private Task OpenAsync() => _open(Summary);

    partial void OnLogoChanging(Bitmap? value)
        => OwnedBitmap.DisposeIfOwned(_logo);

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
        if (string.IsNullOrWhiteSpace(LogoUrl))
        {
            Logo = ModIconAssets.Default;
            return;
        }

        IsLogoLoading = true;
        try
        {
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
