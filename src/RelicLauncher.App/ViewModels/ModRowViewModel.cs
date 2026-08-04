using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed partial class ModRowViewModel : ViewModelBase
{
    private readonly IRemoteImageCache _images;
    private readonly Func<ModSummary?, Task> _open;

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
    }

    public ModSummary Summary { get; }
    public string Name { get; }
    public string Author { get; }
    public string SummaryText { get; }
    public string DownloadsLabel { get; }
    public string? LogoUrl { get; }

    [ObservableProperty]
    private Bitmap? _logo;

    [ObservableProperty]
    private bool _isLogoLoading;

    [RelayCommand]
    private Task OpenAsync() => _open(Summary);

    public async Task LoadLogoAsync()
    {
        if (string.IsNullOrWhiteSpace(LogoUrl) || Logo is not null)
        {
            return;
        }

        IsLogoLoading = true;
        try
        {
            var bytes = await _images.GetImageBytesAsync(LogoUrl).ConfigureAwait(true);
            if (bytes is null)
            {
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
