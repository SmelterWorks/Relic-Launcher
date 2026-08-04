using Avalonia.Media.Imaging;

namespace RelicLauncher.App.Services;

public interface IRemoteNewsImageLoader
{
    Task<Bitmap?> LoadAsync(string url, CancellationToken cancellationToken = default);
}
