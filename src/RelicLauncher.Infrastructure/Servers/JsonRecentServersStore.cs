using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Servers;

public sealed class JsonRecentServersStore : IRecentServersStore
{
    private const int MaxRecents = 10;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IAppPathProvider _pathProvider;
    private readonly ILogger<JsonRecentServersStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonRecentServersStore(IAppPathProvider pathProvider, ILogger<JsonRecentServersStore> logger)
    {
        _pathProvider = pathProvider;
        _logger = logger;
    }

    private string StorePath => Path.Combine(_pathProvider.GetPaths().RootDirectory, "recent-servers.json");

    public async Task<Result<IReadOnlyList<string>>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(StorePath))
            {
                return Result<IReadOnlyList<string>>.Success(Array.Empty<string>());
            }

            var json = await File.ReadAllTextAsync(StorePath, cancellationToken).ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
            return Result<IReadOnlyList<string>>.Success(entries);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogDebug(ex, "Could not read recent servers");
            return Result<IReadOnlyList<string>>.Success(Array.Empty<string>());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = File.Exists(StorePath)
                ? JsonSerializer.Deserialize<List<string>>(
                    await File.ReadAllTextAsync(StorePath, cancellationToken).ConfigureAwait(false),
                    JsonOptions) ?? []
                : [];
            list.RemoveAll(a => string.Equals(a, address, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, address);
            if (list.Count > MaxRecents)
            {
                list = list.Take(MaxRecents).ToList();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            await File.WriteAllTextAsync(StorePath, JsonSerializer.Serialize(list, JsonOptions), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogDebug(ex, "Could not record recent server");
        }
        finally
        {
            _gate.Release();
        }
    }
}
