using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Settings;

public sealed class JsonLauncherSettingsStore : ILauncherSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IAppPathProvider _pathProvider;
    private readonly ILogger<JsonLauncherSettingsStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonLauncherSettingsStore(IAppPathProvider pathProvider, ILogger<JsonLauncherSettingsStore> logger)
    {
        _pathProvider = pathProvider;
        _logger = logger;
    }

    public async Task<Result<LauncherSettings>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var paths = _pathProvider.GetPaths();
            Directory.CreateDirectory(paths.RootDirectory);

            if (!File.Exists(paths.SettingsFile))
            {
                var defaults = new LauncherSettings();
                var write = await WriteUnlockedAsync(paths.SettingsFile, defaults, cancellationToken).ConfigureAwait(false);
                return write.IsSuccess
                    ? Result<LauncherSettings>.Success(defaults)
                    : Result<LauncherSettings>.Failure(write.Error!);
            }

            var stream = new FileStream(paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            try
            {
                var settings = await JsonSerializer.DeserializeAsync<LauncherSettings>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);

                if (settings is null)
                {
                    return Result<LauncherSettings>.Failure("Settings file deserialized to null.");
                }

                if (string.IsNullOrWhiteSpace(settings.SelectedThemeId))
                {
                    settings.SelectedThemeId = LauncherSettings.DefaultThemeId;
                }

                return Result<LauncherSettings>.Success(settings);
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogError(ex, "Failed to load settings");
            return Result<LauncherSettings>.Failure(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result> SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var paths = _pathProvider.GetPaths();
            Directory.CreateDirectory(paths.RootDirectory);
            return await WriteUnlockedAsync(paths.SettingsFile, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogError(ex, "Failed to save settings");
            return Result.Failure(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Result> WriteUnlockedAsync(string path, LauncherSettings settings, CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        try
        {
            var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            try
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to write settings to {Path}", path);
            TryDelete(tempPath);
            return Result.Failure(ex.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup of temp file.
        }
    }
}
