using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed partial class NewsContentBlockViewModel : ViewModelBase
{
    private readonly IRemoteNewsImageLoader _imageLoader;
    private readonly IUrlLauncher _urlLauncher;

    public NewsContentBlockViewModel(NewsContentBlock block, IRemoteNewsImageLoader imageLoader, IUrlLauncher urlLauncher)
    {
        _imageLoader = imageLoader;
        _urlLauncher = urlLauncher;
        Kind = block.Kind;
        Text = block.Text;
        MediaUrl = block.Url;
        Alt = block.Alt;
        ThumbnailUrl = block.ThumbnailUrl ?? block.Url;
        IsText = block.Kind == NewsContentBlockKind.Text;
        IsImage = block.Kind == NewsContentBlockKind.Image;
        IsVideo = block.Kind == NewsContentBlockKind.Video;
    }

    public NewsContentBlockKind Kind { get; }
    public string? Text { get; }
    public string? MediaUrl { get; }
    public string? ThumbnailUrl { get; }
    public string? Alt { get; }
    public bool IsText { get; }
    public bool IsImage { get; }
    public bool IsVideo { get; }

    [ObservableProperty]
    private Bitmap? _image;

    [ObservableProperty]
    private bool _isImageLoading;

    public async Task LoadImageAsync(CancellationToken cancellationToken = default)
    {
        if (!IsImage && !IsVideo)
        {
            return;
        }

        var url = IsVideo ? ThumbnailUrl : MediaUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        IsImageLoading = true;
        Image = await _imageLoader.LoadAsync(url, cancellationToken).ConfigureAwait(true);
        IsImageLoading = false;
    }

    [RelayCommand]
    private void OpenMedia()
    {
        if (!string.IsNullOrWhiteSpace(MediaUrl))
        {
            _urlLauncher.OpenUrl(MediaUrl);
        }
    }
}
