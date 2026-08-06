using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Endpoints;

namespace RelicLauncher.Infrastructure.Auth;

public sealed class AccountAuthService : IAccountAuthService
{
    public const string SessionSecretKey = RelicSecretKeys.AccountSession;
    public const string EmailSecretKey = RelicSecretKeys.AccountEmail;
    public const string LegacyCookieSecretKey = RelicSecretKeys.AccountCookies;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISecretStore _secretStore;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<AccountAuthService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ISessionSignatureValidator _sessionSignatureValidator;
    private StoredSession? _session;

    public AccountAuthService(ISecretStore secretStore, IEndpointProvider endpoints, ILogger<AccountAuthService> logger)
        : this(secretStore, endpoints, logger, CreateHandler())
    {
    }

    internal AccountAuthService(
        ISecretStore secretStore,
        ILogger<AccountAuthService> logger,
        HttpMessageHandler handler,
        ISessionSignatureValidator? sessionSignatureValidator = null)
        : this(secretStore, new EndpointProvider(), logger, handler, sessionSignatureValidator)
    {
    }

    internal AccountAuthService(
        ISecretStore secretStore,
        IEndpointProvider endpoints,
        ILogger<AccountAuthService> logger,
        HttpMessageHandler handler,
        ISessionSignatureValidator? sessionSignatureValidator = null)
    {
        _secretStore = secretStore;
        _endpoints = endpoints;
        _logger = logger;
        _sessionSignatureValidator = sessionSignatureValidator ?? new GameSessionSignatureValidator();
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45),
        };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", "0.1.0"));
        }
    }

    public async Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await RestoreSessionAsync(cancellationToken).ConfigureAwait(false);
        if (_session is null || string.IsNullOrWhiteSpace(_session.Email))
        {
            return Result<AccountSessionStatus>.Success(new AccountSessionStatus { IsSignedIn = false });
        }

        return Result<AccountSessionStatus>.Success(ToStatus(_session));
    }

    public async Task<Result<AccountSessionStatus>> LoginAsync(AccountCredentials credentials, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.Email) || string.IsNullOrWhiteSpace(credentials.Password))
        {
            _logger.LogWarning("Game account login rejected: email or password missing");
            return Result<AccountSessionStatus>.Failure("Email and password are required.");
        }

        try
        {
            var gameLoginVersion = await FetchGameLoginVersionAsync(cancellationToken).ConfigureAwait(false);
            var form = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["email"] = credentials.Email.Trim(),
                ["password"] = credentials.Password,
                ["totpcode"] = credentials.TotpCode?.Trim() ?? string.Empty,
                ["prelogintoken"] = credentials.PreLoginToken?.Trim() ?? string.Empty,
                ["gameloginversion"] = gameLoginVersion,
            };

            using var content = new FormUrlEncodedContent(form);
            using var response = await _httpClient
                .PostAsync(VintageStoryEndpoints.GameLoginUrl, content, cancellationToken)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Game login HTTP {Status}. BodyPreview={Preview}", (int)response.StatusCode, Truncate(body, 240));
                return Result<AccountSessionStatus>.Failure($"Sign-in failed with HTTP {(int)response.StatusCode}.");
            }

            GameLoginResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<GameLoginResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Game login returned non-JSON. BodyPreview={Preview}", Truncate(body, 240));
                return Result<AccountSessionStatus>.Failure("Sign-in failed. Unexpected response from the game auth server.");
            }

            if (parsed is null)
            {
                return Result<AccountSessionStatus>.Failure("Sign-in failed. Empty response from the game auth server.");
            }

            if (parsed.Valid == 1)
            {
                return await CompleteLoginAsync(credentials.Email.Trim(), parsed, cancellationToken).ConfigureAwait(false);
            }

            return HandleLoginFailure(parsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogError(ex, "Game account login request failed");
            return Result<AccountSessionStatus>.Failure("Network error during sign-in: " + ex.Message);
        }
    }

    public async Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
    {
        _session = null;
        await _secretStore.DeleteAsync(SessionSecretKey, cancellationToken).ConfigureAwait(false);
        await _secretStore.DeleteAsync(EmailSecretKey, cancellationToken).ConfigureAwait(false);
        await _secretStore.DeleteAsync(LegacyCookieSecretKey, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Game account signed out");
        return Result.Success();
    }

    public async Task<Result> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsSuccess)
        {
            return Result.Failure(status.Error ?? "Could not read account status.");
        }

        if (status.Value!.IsSignedIn)
        {
            return Result.Success();
        }

        return Result.Failure("Sign in with your Vintage Story game account in Settings.");
    }

    public async Task<Result> ValidateSessionAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsSuccess)
        {
            return Result.Failure(status.Error ?? "Could not read account status.");
        }

        if (!status.Value!.IsSignedIn)
        {
            return Result.Failure("Sign in with your Vintage Story game account in Settings.");
        }

        if (!_sessionSignatureValidator.IsValid(status.Value.SessionKey, status.Value.SessionSignature, status.Value.PlayerUid))
        {
            _logger.LogWarning("Saved game session failed local signature check for {Player}", status.Value.PlayerName);
            await ClearSessionInternalAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure("Your saved Vintage Story session is invalid. Sign in again in Settings.");
        }

        try
        {
            return await ValidateWithServerAsync(status.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogDebug(ex, "Session validate request failed; continuing offline with cached session.");
            return Result.Success();
        }
    }

    private async Task<Result> ValidateWithServerAsync(AccountSessionStatus status, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["uid"] = status.PlayerUid ?? string.Empty,
            ["sessionkey"] = status.SessionKey ?? string.Empty,
        };

        using var content = new FormUrlEncodedContent(form);
        using var response = await _httpClient
            .PostAsync(VintageStoryEndpoints.ClientValidateUrl, content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Session validate HTTP {Status}; continuing offline with cached session.", (int)response.StatusCode);
            return Result.Success();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        ClientValidateResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ClientValidateResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Session validate returned non-JSON; continuing offline with cached session.");
            return Result.Success();
        }

        if (parsed is null)
        {
            return Result.Success();
        }

        if (parsed.Valid == 1)
        {
            await UpdateFromValidateAsync(parsed, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        _logger.LogInformation("Game session rejected by server. Reason={Reason}", parsed.Reason);
        await ClearSessionInternalAsync(cancellationToken).ConfigureAwait(false);
        return Result.Failure("Your Vintage Story session expired. Sign in again in Settings.");
    }

    private Result<AccountSessionStatus> HandleLoginFailure(GameLoginResponse parsed)
    {
        var reason = parsed.Reason?.Trim() ?? string.Empty;
        if (string.Equals(reason, "requiretotpcode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reason, "wrongtotpcode", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(parsed.PreLoginToken) &&
                string.Equals(reason, "requiretotpcode", StringComparison.OrdinalIgnoreCase))
            {
                return Result<AccountSessionStatus>.Failure("Two-factor login required, but no pre-login token was returned.");
            }

            _logger.LogInformation("Game login requires TOTP. Reason={Reason}", reason);
            return Result<AccountSessionStatus>.Success(new AccountSessionStatus
            {
                IsSignedIn = false,
                RequiresTotp = true,
                PreLoginToken = parsed.PreLoginToken,
            });
        }

        var message = reason switch
        {
            "invalidemailorpassword" => "Wrong email or password (game account, not forum).",
            "ipchanged" => "Sign-in blocked because your IP changed. Try again from the official client once, then retry here.",
            "temporarilyblocked" => "This account is temporarily blocked from signing in.",
            _ => string.IsNullOrWhiteSpace(reason)
                ? "Sign-in failed."
                : "Sign-in failed: " + reason,
        };
        _logger.LogWarning("Game login failed. Reason={Reason}", reason);
        return Result<AccountSessionStatus>.Failure(message);
    }

    private async Task<Result<AccountSessionStatus>> CompleteLoginAsync(
        string email,
        GameLoginResponse parsed,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parsed.SessionKey) ||
            string.IsNullOrWhiteSpace(parsed.SessionSignature) ||
            string.IsNullOrWhiteSpace(parsed.Uid) ||
            string.IsNullOrWhiteSpace(parsed.PlayerName))
        {
            return Result<AccountSessionStatus>.Failure("Sign-in succeeded but the auth server omitted session fields.");
        }

        var session = new StoredSession
        {
            Email = email,
            PlayerName = parsed.PlayerName,
            PlayerUid = parsed.Uid,
            SessionKey = parsed.SessionKey,
            SessionSignature = parsed.SessionSignature,
            Entitlements = FormatEntitlements(parsed.Entitlements),
            MpToken = FormatOptional(parsed.MpToken),
            HostGameServer = FormatHostGameServer(parsed.HasGameServer),
        };

        var persist = await PersistSessionAsync(session, cancellationToken).ConfigureAwait(false);
        if (!persist.IsSuccess)
        {
            _logger.LogError("Game login succeeded remotely but session persist failed: {Error}", persist.Error);
            return Result<AccountSessionStatus>.Failure(
                "Signed in on the server but Relic could not save the session: " + (persist.Error ?? "unknown error"));
        }

        _session = session;
        await _secretStore.DeleteAsync(LegacyCookieSecretKey, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Game account login succeeded for {Email} as {Player}", email, session.PlayerName);
        return Result<AccountSessionStatus>.Success(ToStatus(session));
    }

    private async Task RestoreSessionAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return;
        }

        var sessionResult = await _secretStore.GetAsync(SessionSecretKey, cancellationToken).ConfigureAwait(false);
        if (!sessionResult.IsSuccess)
        {
            _logger.LogWarning("Could not read saved game session: {Error}", sessionResult.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(sessionResult.Value))
        {
            return;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredSession>(sessionResult.Value, JsonOptions);
            if (stored is null ||
                string.IsNullOrWhiteSpace(stored.Email) ||
                string.IsNullOrWhiteSpace(stored.SessionKey) ||
                string.IsNullOrWhiteSpace(stored.SessionSignature) ||
                string.IsNullOrWhiteSpace(stored.PlayerUid))
            {
                return;
            }

            _session = stored;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to restore game account session");
        }
    }

    private async Task ClearSessionInternalAsync(CancellationToken cancellationToken)
    {
        _session = null;
        await _secretStore.DeleteAsync(SessionSecretKey, cancellationToken).ConfigureAwait(false);
        await _secretStore.DeleteAsync(EmailSecretKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateFromValidateAsync(ClientValidateResponse parsed, CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return;
        }

        var updated = new StoredSession
        {
            Email = _session.Email,
            PlayerName = _session.PlayerName,
            PlayerUid = _session.PlayerUid,
            SessionKey = _session.SessionKey,
            SessionSignature = _session.SessionSignature,
            Entitlements = FormatEntitlements(parsed.Entitlements) is { Length: > 0 } entitlements
                ? entitlements
                : _session.Entitlements,
            MpToken = string.Empty,
            HostGameServer = FormatHostGameServer(parsed.HasGameServer) is { Length: > 0 } hostGameServer
                ? hostGameServer
                : _session.HostGameServer,
        };

        var persist = await PersistSessionAsync(updated, cancellationToken).ConfigureAwait(false);
        if (persist.IsSuccess)
        {
            _session = updated;
        }
    }

    private async Task<Result> PersistSessionAsync(StoredSession session, CancellationToken cancellationToken)
    {
        var emailSave = await _secretStore.SetAsync(EmailSecretKey, session.Email, cancellationToken).ConfigureAwait(false);
        if (!emailSave.IsSuccess)
        {
            return emailSave;
        }

        var json = JsonSerializer.Serialize(session);
        return await _secretStore.SetAsync(SessionSecretKey, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> FetchGameLoginVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var text = await _httpClient.GetStringAsync(VintageStoryEndpoints.LatestUnstableUrl, cancellationToken)
                .ConfigureAwait(false);
            var version = text.Trim();
            return string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogDebug(ex, "Could not fetch latestunstable for gameloginversion");
            return "1.0.0";
        }
    }

    private static AccountSessionStatus ToStatus(StoredSession session)
        => new()
        {
            IsSignedIn = true,
            Email = session.Email,
            PlayerName = session.PlayerName,
            PlayerUid = session.PlayerUid,
            SessionKey = session.SessionKey,
            SessionSignature = session.SessionSignature,
            Entitlements = session.Entitlements,
            MpToken = session.MpToken,
            HostGameServer = session.HostGameServer,
        };

    private static string FormatEntitlements(JsonElement? entitlements)
    {
        if (entitlements is null)
        {
            return string.Empty;
        }

        var value = entitlements.Value;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
            _ => value.GetRawText(),
        };
    }

    private static string FormatOptional(JsonElement? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var element = value.Value;
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
            _ => element.GetRawText(),
        };
    }

    private static string FormatHostGameServer(JsonElement? hasGameServer)
    {
        if (hasGameServer is null)
        {
            return string.Empty;
        }

        var element = hasGameServer.Value;
        return element.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            _ => string.Empty,
        };
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value;
        }

        return value[..max] + "...";
    }

    private static HttpMessageHandler CreateHandler()
        => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        };

    private sealed class StoredSession
    {
        public string Email { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string PlayerUid { get; set; } = string.Empty;
        public string SessionKey { get; set; } = string.Empty;
        public string SessionSignature { get; set; } = string.Empty;
        public string Entitlements { get; set; } = string.Empty;
        public string MpToken { get; set; } = string.Empty;
        public string HostGameServer { get; set; } = string.Empty;
    }

    private sealed class GameLoginResponse
    {
        [JsonPropertyName("valid")]
        public int Valid { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("prelogintoken")]
        public string? PreLoginToken { get; set; }

        [JsonPropertyName("sessionkey")]
        public string? SessionKey { get; set; }

        [JsonPropertyName("sessionsignature")]
        public string? SessionSignature { get; set; }

        [JsonPropertyName("uid")]
        public string? Uid { get; set; }

        [JsonPropertyName("playername")]
        public string? PlayerName { get; set; }

        [JsonPropertyName("entitlements")]
        public JsonElement? Entitlements { get; set; }

        [JsonPropertyName("mptoken")]
        public JsonElement? MpToken { get; set; }

        [JsonPropertyName("hasgameserver")]
        public JsonElement? HasGameServer { get; set; }
    }

    private sealed class ClientValidateResponse
    {
        [JsonPropertyName("valid")]
        public int Valid { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("entitlements")]
        public JsonElement? Entitlements { get; set; }

        [JsonPropertyName("hasgameserver")]
        public JsonElement? HasGameServer { get; set; }
    }

}
