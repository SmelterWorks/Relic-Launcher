using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.IO;

internal static class BoundedStreamCopy
{
    public static async Task<Result> CopyAsync(
        Stream input,
        Stream output,
        long? contentLength,
        long maxBytes,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (contentLength is > 0 && contentLength.Value > maxBytes)
        {
            return Result.Failure($"Download is larger than the allowed maximum ({FormatBytes(maxBytes)}).");
        }

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            readTotal += read;
            if (readTotal > maxBytes)
            {
                return Result.Failure($"Download exceeded the allowed maximum ({FormatBytes(maxBytes)}).");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (contentLength is > 0)
            {
                progress?.Report(Math.Clamp(readTotal / (double)contentLength.Value, 0, 0.99));
            }
            else
            {
                progress?.Report(0.5);
            }
        }

        progress?.Report(1.0);
        return Result.Success();
    }

    private static string FormatBytes(long bytes)
    {
        const double mib = 1024d * 1024d;
        const double gib = mib * 1024d;
        if (bytes >= gib)
        {
            return $"{bytes / gib:0.#} GiB";
        }

        return $"{bytes / mib:0.#} MiB";
    }
}
