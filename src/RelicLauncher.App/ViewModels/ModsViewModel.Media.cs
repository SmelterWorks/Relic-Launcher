using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RelicLauncher.App.Services;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Mods;

namespace RelicLauncher.App.ViewModels;

public partial class ModsViewModel
{
    private async Task LoadDetailMediaAsync(ModDetails details, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(details.LogoUrl))
        {
            var bytes = await _images.GetImageBytesAsync(details.LogoUrl).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(SelectedDetails, details))
            {
                return;
            }

            DetailLogo = bytes is null
                ? ModIconAssets.Default
                : ScaledBitmapLoader.FromBytes(bytes, RelicDefaults.DecodeWidthModDetailLogo)
                  ?? ModIconAssets.Default;
        }
        else
        {
            DetailLogo = ModIconAssets.Default;
        }

        ClearScreenshotItems();
        var shots = details.Screenshots.Take(8).ToArray();
        if (shots.Length == 0)
        {
            return;
        }

        using var gate = new SemaphoreSlim(4);
        var tasks = new Task<ModImageItemViewModel?>[shots.Length];
        for (var i = 0; i < shots.Length; i++)
        {
            var shot = shots[i];
            tasks[i] = LoadScreenshotItemAsync(details, shot, gate, cancellationToken);
        }

        var items = await Task.WhenAll(tasks).ConfigureAwait(true);
        if (cancellationToken.IsCancellationRequested || !ReferenceEquals(SelectedDetails, details))
        {
            foreach (var item in items)
            {
                item?.Dispose();
            }

            return;
        }

        foreach (var item in items)
        {
            if (item is not null)
            {
                ScreenshotItems.Add(item);
            }
        }
    }

    private async Task<ModImageItemViewModel?> LoadScreenshotItemAsync(
        ModDetails details,
        ModScreenshot shot,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        var thumbUrl = shot.ThumbnailUrl ?? shot.MainUrl;
        if (string.IsNullOrWhiteSpace(thumbUrl))
        {
            return null;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(SelectedDetails, details))
            {
                return null;
            }

            var bytes = await _images.GetImageBytesAsync(thumbUrl).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(SelectedDetails, details))
            {
                return null;
            }

            if (bytes is null)
            {
                return null;
            }

            var bitmap = ScaledBitmapLoader.FromBytes(bytes, RelicDefaults.DecodeWidthScreenshotThumb);
            return bitmap is null
                ? null
                : new ModImageItemViewModel(bitmap, shot.MainUrl ?? thumbUrl, OpenScreenshotAsync);
        }
        finally
        {
            gate.Release();
        }
    }

    [RelayCommand]
    private async Task OpenDetailLogoAsync()
    {
        if (DetailLogo is null)
        {
            return;
        }

        var url = SelectedDetails?.LogoUrl;
        if (!string.IsNullOrWhiteSpace(url))
        {
            await OpenImageUrlAsync(url).ConfigureAwait(true);
            return;
        }

        SetViewerImage(DetailLogo, ownsImage: false);
        IsImageViewerOpen = true;
    }

    private Task OpenScreenshotAsync(ModImageItemViewModel item)
        => OpenImageUrlAsync(item.FullUrl, item.Thumbnail);

    private async Task OpenImageUrlAsync(string? url, Bitmap? fallback = null)
    {
        IsImageViewerOpen = true;
        IsViewerLoading = true;
        SetViewerImage(fallback, ownsImage: false);
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            var bytes = await _images.GetImageBytesAsync(url).ConfigureAwait(true);
            if (bytes is null)
            {
                return;
            }

            var bitmap = ScaledBitmapLoader.FromBytes(bytes, RelicDefaults.DecodeWidthImageViewer);
            if (bitmap is not null)
            {
                SetViewerImage(bitmap, ownsImage: true);
            }
        }
        finally
        {
            IsViewerLoading = false;
        }
    }

    [RelayCommand]
    private void CloseImageViewer()
    {
        IsImageViewerOpen = false;
        IsViewerLoading = false;
        SetViewerImage(null, ownsImage: false);
    }

    public void UnloadMedia()
    {
        CloseImageViewer();
        ClearScreenshotItems();
        DetailLogo = ModIconAssets.Default;
        foreach (var row in BrowseResults)
        {
            row.UnloadLogo();
        }

        foreach (var row in _allInstalledRows)
        {
            row.UnloadLogo();
        }
    }

    partial void OnDetailLogoChanging(Bitmap? value)
        => OwnedBitmap.DisposeIfOwned(_detailLogo);

    private void SetViewerImage(Bitmap? image, bool ownsImage)
    {
        if (_viewerOwnsImage)
        {
            OwnedBitmap.DisposeIfOwned(ViewerImage);
        }

        _viewerOwnsImage = ownsImage && image is not null;
        ViewerImage = image;
    }

    private void ClearBrowseResults()
    {
        foreach (var row in BrowseResults)
        {
            row.UnloadLogo();
        }

        BrowseResults.Clear();
        SelectedBrowseMod = null;
        SelectedDetails = null;
        SelectedRelease = null;
    }

    private void ClearScreenshotItems()
    {
        foreach (var item in ScreenshotItems)
        {
            item.Dispose();
        }

        ScreenshotItems.Clear();
    }
}
