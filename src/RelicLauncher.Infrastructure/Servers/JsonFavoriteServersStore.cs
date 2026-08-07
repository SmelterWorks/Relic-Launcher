using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Servers;

public sealed class JsonFavoriteServersStore : IFavoriteServersStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly IAppPathProvider _pathProvider;
    private readonly ILogger<JsonFavoriteServersStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonFavoriteServersStore(IAppPathProvider pathProvider, ILogger<JsonFavoriteServersStore> logger)
    {
        _pathProvider = pathProvider;
        _logger = logger;
    }

    private string StorePath => Path.Combine(_pathProvider.GetPaths().RootDirectory, "favorite-servers.json");

    public async Task<Result<IReadOnlyList<FavoriteServerEntry>>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(StorePath))
            {
                return Result<IReadOnlyList<FavoriteServerEntry>>.Success(Array.Empty<FavoriteServerEntry>());
            }

            var json = await File.ReadAllTextAsync(StorePath, cancellationToken).ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize<List<FavoriteServerEntry>>(json, JsonOptions) ?? [];
            return Result<IReadOnlyList<FavoriteServerEntry>>.Success(entries);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read favorite servers");
            return Result<IReadOnlyList<FavoriteServerEntry>>.Failure("Could not read favorite servers.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result> AddAsync(FavoriteServerEntry entry, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = await ReadAllUnsafeAsync(cancellationToken).ConfigureAwait(false);
            list.RemoveAll(e => string.Equals(e.Address, entry.Address, StringComparison.OrdinalIgnoreCase));
            list.Add(entry);
            await WriteAllUnsafeAsync(list, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Could not save favorite server");
            return Result.Failure("Could not save favorite server.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result> RemoveAsync(string address, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = await ReadAllUnsafeAsync(cancellationToken).ConfigureAwait(false);
            list.RemoveAll(e => string.Equals(e.Address, address, StringComparison.OrdinalIgnoreCase));
            await WriteAllUnsafeAsync(list, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Could not remove favorite server");
            return Result.Failure("Could not remove favorite server.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<FavoriteServerEntry>> ReadAllUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StorePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(StorePath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<FavoriteServerEntry>>(json, JsonOptions) ?? [];
    }

    private async Task WriteAllUnsafeAsync(List<FavoriteServerEntry> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        await File.WriteAllTextAsync(StorePath, json, cancellationToken).ConfigureAwait(false);
    }
}
