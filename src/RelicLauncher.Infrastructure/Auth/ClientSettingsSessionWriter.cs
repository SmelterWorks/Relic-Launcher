using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Auth;

public sealed class ClientSettingsSessionWriter : IClientSettingsSessionWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly string[] SessionFieldNames =
    [
        "sessionkey",
        "sessionsignature",
        "useremail",
        "playeruid",
        "playername",
        "entitlements",
        "mptoken",
        "hostgameserver",
    ];

    private readonly IAccountAuthService _accountAuth;
    private readonly ILogger<ClientSettingsSessionWriter> _logger;

    public ClientSettingsSessionWriter(IAccountAuthService accountAuth, ILogger<ClientSettingsSessionWriter> logger)
    {
        _accountAuth = accountAuth;
        _logger = logger;
    }

    public async Task<Result> ApplySessionAsync(string dataPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            return Result.Failure("Data path is empty.");
        }

        var status = await _accountAuth.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsSuccess)
        {
            return Result.Failure(status.Error ?? "Could not read account status.");
        }

        if (!status.Value!.IsSignedIn ||
            string.IsNullOrWhiteSpace(status.Value.SessionKey) ||
            string.IsNullOrWhiteSpace(status.Value.SessionSignature) ||
            string.IsNullOrWhiteSpace(status.Value.PlayerUid))
        {
            return Result.Success();
        }

        try
        {
            Directory.CreateDirectory(dataPath);
            var path = Path.Combine(dataPath, "clientsettings.json");
            JsonObject root;
            if (File.Exists(path))
            {
                var existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                root = string.IsNullOrWhiteSpace(existing)
                    ? new JsonObject()
                    : JsonNode.Parse(existing) as JsonObject ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var stringSettings = root["stringSettings"] as JsonObject ?? new JsonObject();
            root["stringSettings"] = stringSettings;

            stringSettings["sessionkey"] = status.Value.SessionKey;
            stringSettings["sessionsignature"] = status.Value.SessionSignature;
            stringSettings["useremail"] = status.Value.Email ?? string.Empty;
            stringSettings["playeruid"] = status.Value.PlayerUid;
            stringSettings["playername"] = status.Value.PlayerName ?? string.Empty;
            stringSettings["entitlements"] = status.Value.Entitlements ?? string.Empty;
            stringSettings["mptoken"] = status.Value.MpToken ?? string.Empty;
            stringSettings["hostgameserver"] = status.Value.HostGameServer ?? string.Empty;

            var json = root.ToJsonString(WriteOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Wrote game session into {Path} for {Player}", path, status.Value.PlayerName);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Failed writing clientsettings session under {DataPath}", dataPath);
            return Result.Failure("Could not write game session into clientsettings.json: " + ex.Message);
        }
    }

    public async Task<Result> ClearSessionAsync(string dataPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            return Result.Failure("Data path is empty.");
        }

        var path = Path.Combine(dataPath, "clientsettings.json");
        if (!File.Exists(path))
        {
            return Result.Success();
        }

        try
        {
            var existing = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(existing))
            {
                return Result.Success();
            }

            if (JsonNode.Parse(existing) is not JsonObject root ||
                root["stringSettings"] is not JsonObject stringSettings)
            {
                return Result.Success();
            }

            foreach (var field in SessionFieldNames)
            {
                stringSettings[field] = string.Empty;
            }

            var json = root.ToJsonString(WriteOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cleared game session in {Path}", path);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Failed clearing clientsettings session under {DataPath}", dataPath);
            return Result.Failure("Could not clear game session in clientsettings.json: " + ex.Message);
        }
    }
}
