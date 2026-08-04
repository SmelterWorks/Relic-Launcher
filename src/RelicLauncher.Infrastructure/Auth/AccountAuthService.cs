using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Constants;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;
using RelicLauncher.Infrastructure.Endpoints;

namespace RelicLauncher.Infrastructure.Auth;

public sealed class AccountAuthService : IAccountAuthService
{
    public const string CookieSecretKey = RelicSecretKeys.AccountCookies;
    public const string EmailSecretKey = RelicSecretKeys.AccountEmail;

    private readonly ISecretStore _secretStore;
    private readonly IEndpointProvider _endpoints;
    private readonly ILogger<AccountAuthService> _logger;
    private readonly CookieContainer _cookies;
    private readonly HttpClient _httpClient;
    private string? _email;

    public AccountAuthService(ISecretStore secretStore, IEndpointProvider endpoints, ILogger<AccountAuthService> logger)
        : this(secretStore, endpoints, logger, CreateHandler(out var cookies), cookies)
    {
    }

    internal AccountAuthService(
        ISecretStore secretStore,
        ILogger<AccountAuthService> logger,
        HttpMessageHandler handler,
        CookieContainer cookies)
        : this(secretStore, new EndpointProvider(), logger, handler, cookies)
    {
    }

    internal AccountAuthService(
        ISecretStore secretStore,
        IEndpointProvider endpoints,
        ILogger<AccountAuthService> logger,
        HttpMessageHandler handler,
        CookieContainer cookies)
    {
        _secretStore = secretStore;
        _endpoints = endpoints;
        _logger = logger;
        _cookies = cookies;
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45),
        };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RelicLauncher", "0.1.0"));
        }
    }

    private Uri AccountBaseUri => new(_endpoints.AccountBaseUrl);

    public HttpClient HttpClient => _httpClient;

    public CookieContainer CookieContainer => _cookies;

    public async Task<Result<AccountSessionStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await RestoreSessionAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(_email))
        {
            return Result<AccountSessionStatus>.Success(new AccountSessionStatus { IsSignedIn = false });
        }

        return Result<AccountSessionStatus>.Success(new AccountSessionStatus
        {
            IsSignedIn = true,
            Email = _email,
        });
    }

    public async Task<Result<AccountSessionStatus>> LoginAsync(AccountCredentials credentials, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.Email) || string.IsNullOrWhiteSpace(credentials.Password))
        {
            _logger.LogWarning("Account login rejected: email or password missing");
            return Result<AccountSessionStatus>.Failure("Email and password are required.");
        }

        try
        {
            foreach (Cookie cookie in _cookies.GetCookies(AccountBaseUri).Cast<Cookie>().ToList())
            {
                cookie.Expired = true;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["email"] = credentials.Email.Trim(),
                ["password"] = credentials.Password,
                ["loginredir"] = string.Empty,
            });

            using var response = await _httpClient.PostAsync(new Uri(AccountBaseUri, "attemptlogin"), content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var location = response.Headers.Location?.ToString();
            var cookieNames = string.Join(", ",
                _cookies.GetCookies(AccountBaseUri).Cast<Cookie>().Select(c => c.Name).Distinct(StringComparer.Ordinal));

            if (body.Contains("Captcha verification failed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Account login blocked by captcha. Status={Status}", (int)response.StatusCode);
                return Result<AccountSessionStatus>.Failure(
                    "The account portal requires a captcha. Use Sign in with browser in Settings (in-app account page), then click Use this session.");
            }

            if (!LooksSignedIn(response, body, location))
            {
                return FailLogin(response, body, location, cookieNames);
            }

            return await CompleteLoginAsync(credentials.Email.Trim(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogError(ex, "Account login request failed");
            return Result<AccountSessionStatus>.Failure("Network error during sign-in: " + ex.Message);
        }
    }


    private Result<AccountSessionStatus> FailLogin(HttpResponseMessage response, string body, string? location, string cookieNames)
    {
        var reason = ExplainLoginFailure(response, body, location);
        _logger.LogWarning(
            "Account login failed. Status={Status} Location={Location} Cookies=[{Cookies}] Reason={Reason} BodyPreview={Preview}",
            (int)response.StatusCode,
            location ?? "(none)",
            cookieNames,
            reason,
            Truncate(body, 240));
        return Result<AccountSessionStatus>.Failure(reason);
    }

    private async Task<Result<AccountSessionStatus>> CompleteLoginAsync(string email, CancellationToken cancellationToken)
    {
        _email = email;
        var persist = await PersistSessionAsync(cancellationToken).ConfigureAwait(false);
        if (!persist.IsSuccess)
        {
            _logger.LogError("Account login succeeded remotely but session persist failed: {Error}", persist.Error);
            _email = null;
            return Result<AccountSessionStatus>.Failure(
                "Signed in on the server but Relic could not save the session: " + (persist.Error ?? "unknown error"));
        }

        _logger.LogInformation("Account login succeeded for {Email}", _email);
        return Result<AccountSessionStatus>.Success(new AccountSessionStatus
        {
            IsSignedIn = true,
            Email = _email,
        });
    }

    public async Task<Result<AccountSessionStatus>> ImportBrowserSessionAsync(
        string email,
        IReadOnlyList<Cookie> cookies,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<AccountSessionStatus>.Failure("Email is required to label the saved session.");
        }

        if (cookies.Count == 0)
        {
            return Result<AccountSessionStatus>.Failure("No cookies were provided from the browser session.");
        }

        foreach (Cookie cookie in _cookies.GetCookies(AccountBaseUri).Cast<Cookie>().ToList())
        {
            cookie.Expired = true;
        }

        foreach (var cookie in cookies)
        {
            try
            {
                var domain = string.IsNullOrWhiteSpace(cookie.Domain) ? ".vintagestory.at" : cookie.Domain;
                var host = domain.StartsWith(".", StringComparison.Ordinal) ? domain.TrimStart('.') : domain;
                _cookies.Add(new Uri("https://" + host + "/"), new Cookie(cookie.Name, cookie.Value)
                {
                    Domain = domain,
                    Path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
                    HttpOnly = cookie.HttpOnly,
                    Secure = cookie.Secure,
                });
            }
            catch (CookieException ex)
            {
                _logger.LogDebug(ex, "Skipped invalid browser cookie {Name}", cookie.Name);
            }
        }

        return await CompleteLoginAsync(email.Trim(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
    {
        _email = null;
        foreach (Cookie cookie in _cookies.GetCookies(AccountBaseUri))
        {
            cookie.Expired = true;
        }

        await _secretStore.DeleteAsync(CookieSecretKey, cancellationToken).ConfigureAwait(false);
        await _secretStore.DeleteAsync(EmailSecretKey, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Account signed out");
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

    private async Task RestoreSessionAsync(CancellationToken cancellationToken)
    {
        if (_email is not null)
        {
            return;
        }

        var emailResult = await _secretStore.GetAsync(EmailSecretKey, cancellationToken).ConfigureAwait(false);
        var cookieResult = await _secretStore.GetAsync(CookieSecretKey, cancellationToken).ConfigureAwait(false);
        if (!emailResult.IsSuccess || !cookieResult.IsSuccess)
        {
            if (!emailResult.IsSuccess)
            {
                _logger.LogWarning("Could not read saved account email: {Error}", emailResult.Error);
            }

            if (!cookieResult.IsSuccess)
            {
                _logger.LogWarning("Could not read saved account cookies: {Error}", cookieResult.Error);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(emailResult.Value))
        {
            return;
        }

        _email = emailResult.Value;
        if (string.IsNullOrWhiteSpace(cookieResult.Value))
        {
            return;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<CookieDto>>(cookieResult.Value) ?? [];
            foreach (var entry in entries)
            {
                var domainUri = entry.Domain.StartsWith("http", StringComparison.Ordinal)
                    ? entry.Domain
                    : "https://" + entry.Domain.TrimStart('.');
                _cookies.Add(new Uri(domainUri), new Cookie(entry.Name, entry.Value)
                {
                    Domain = entry.Domain,
                    Path = entry.Path ?? "/",
                    HttpOnly = entry.HttpOnly,
                    Secure = entry.Secure,
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to restore account cookies");
        }
    }

    private async Task<Result> PersistSessionAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_email))
        {
            var emailSave = await _secretStore.SetAsync(EmailSecretKey, _email, cancellationToken).ConfigureAwait(false);
            if (!emailSave.IsSuccess)
            {
                return emailSave;
            }
        }

        var cookies = new List<CookieDto>();
        CollectCookies(AccountBaseUri, cookies);
        CollectCookies(new Uri(_endpoints.CdnBaseUrl), cookies);

        var json = JsonSerializer.Serialize(cookies);
        return await _secretStore.SetAsync(CookieSecretKey, json, cancellationToken).ConfigureAwait(false);
    }

    private void CollectCookies(Uri uri, List<CookieDto> cookies)
    {
        foreach (Cookie cookie in _cookies.GetCookies(uri))
        {
            if (cookie.Expired)
            {
                continue;
            }

            cookies.Add(new CookieDto
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Domain = cookie.Domain,
                Path = cookie.Path,
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure,
            });
        }
    }

    internal static bool LooksSignedIn(HttpResponseMessage response, string body, string? location)
    {
        var code = (int)response.StatusCode;
        if (code is >= 300 and < 400)
        {
            if (!string.IsNullOrWhiteSpace(location) &&
                location.Contains("attemptlogin", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        if (HasLoginForm(body))
        {
            return false;
        }

        if (ContainsAuthFailurePhrase(body))
        {
            return false;
        }

        return response.IsSuccessStatusCode;
    }

    private static string ExplainLoginFailure(HttpResponseMessage response, string body, string? location)
    {
        if (body.Contains("Captcha verification failed", StringComparison.OrdinalIgnoreCase))
        {
            return "The account portal requires a captcha. Use Sign in with browser in Settings.";
        }

        if (ContainsAuthFailurePhrase(body))
        {
            return "Sign-in failed. Check email and password (game account, not forum).";
        }

        if (HasLoginForm(body))
        {
            return "Sign-in failed. The account portal returned the login form again. Check email and password (game account, not forum).";
        }

        if ((int)response.StatusCode is >= 400)
        {
            return $"Sign-in failed with HTTP {(int)response.StatusCode}.";
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            return $"Sign-in failed after redirect to {location}.";
        }

        return "Sign-in failed. Check email and password (game account, not forum).";
    }

    private static bool HasLoginForm(string body)
        => body.Contains("attemptlogin", StringComparison.OrdinalIgnoreCase) &&
           body.Contains("type=\"password\"", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAuthFailurePhrase(string body)
        => body.Contains("Sign-in failed", StringComparison.OrdinalIgnoreCase) ||
           body.Contains("Invalid email or password", StringComparison.OrdinalIgnoreCase) ||
           body.Contains("incorrect password", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value;
        }

        return value[..max] + "...";
    }

    private static HttpMessageHandler CreateHandler(out CookieContainer cookies)
    {
        cookies = new CookieContainer();
        return new HttpClientHandler
        {
            CookieContainer = cookies,
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false,
        };
    }

    private sealed class CookieDto
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string? Path { get; set; }
        public bool HttpOnly { get; set; }
        public bool Secure { get; set; }
    }
}
