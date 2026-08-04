namespace RelicLauncher.Core.Abstractions;

public interface IRemoteImageCache
{
    Task<byte[]?> GetImageBytesAsync(string url, CancellationToken cancellationToken = default);
}
