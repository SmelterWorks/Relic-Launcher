using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Sandbox;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.Infrastructure.Launch;

public sealed partial class GameLaunchService : IGameLaunchService
{
    private readonly ISandboxBrokerClient _broker;
    private readonly IRuntimePlatform _platform;
    private readonly IClientSettingsSessionWriter _sessionWriter;
    private readonly IDotNetRuntimeProvisioner _runtimeProvisioner;
    private readonly IAccountAuthService _accountAuth;
    private readonly ILogger<GameLaunchService> _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private int? _processId;
    private string? _runningVersion;
    private Task? _exitMonitorTask;
    private bool _isStopping;

    public GameLaunchService(
        ISandboxBrokerClient broker,
        IRuntimePlatform platform,
        IClientSettingsSessionWriter sessionWriter,
        IDotNetRuntimeProvisioner runtimeProvisioner,
        IAccountAuthService accountAuth,
        ILogger<GameLaunchService> logger)
    {
        _broker = broker;
        _platform = platform;
        _sessionWriter = sessionWriter;
        _runtimeProvisioner = runtimeProvisioner;
        _accountAuth = accountAuth;
        _logger = logger;
    }

    public bool IsRunning { get; private set; }

    public bool IsStopping => _isStopping;

    public string? RunningVersion => _runningVersion;

    public event EventHandler? StateChanged;

    public Task<Result<GameInstallInfo>> ResolveAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InstallsRoot))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure("Installs root is not configured. Set it in Settings."));
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure("No game version is selected. Install and select one on the Versions page."));
        }

        var installPath = GameInstallLayout.GetVersionDirectory(request.InstallsRoot, request.Version);
        if (!Directory.Exists(installPath))
        {
            return Task.FromResult(Result<GameInstallInfo>.Failure($"Version {request.Version} is not installed."));
        }

        var exe = VintageStoryExecutableLocator.FindClientExecutable(installPath);
        return Task.FromResult(Result<GameInstallInfo>.Success(new GameInstallInfo
        {
            InstallPath = installPath,
            DetectedVersion = request.Version,
            ExecutableFound = exe is not null,
            ExecutablePath = exe,
        }));
    }

    public async Task<Result> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning)
            {
                return Result.Failure("Vintage Story is already running.");
            }

            var prepared = await PrepareLaunchAsync(request, cancellationToken).ConfigureAwait(false);
            if (!prepared.IsSuccess)
            {
                return Result.Failure(prepared.Error ?? "Could not prepare launch.");
            }

            var (info, args, environment) = prepared.Value!;
            var launch = await _broker.LaunchSandboxedAsync(
                new SandboxLaunchRequest
                {
                    Kind = SandboxKind.GameClient,
                    ExecutablePath = info.ExecutablePath!,
                    Arguments = args,
                    Environment = environment,
                    WorkingDirectory = Path.GetDirectoryName(info.ExecutablePath),
                },
                cancellationToken).ConfigureAwait(false);

            if (!launch.IsSuccess)
            {
                return Result.Failure(launch.Error ?? "Could not start the game client.");
            }

            AttachProcess(launch.Value!.ProcessId, request.Version);
            return Result.Success();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task<Result<(GameInstallInfo Info, List<string> Args, Dictionary<string, string?> Environment)>> PrepareLaunchAsync(
        GameLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        if (!resolved.IsSuccess)
        {
            return Result<(GameInstallInfo, List<string>, Dictionary<string, string?>)>.Failure(resolved.Error ?? "Could not resolve install.");
        }

        var info = resolved.Value!;
        if (!info.ExecutableFound || string.IsNullOrWhiteSpace(info.ExecutablePath))
        {
            return Result<(GameInstallInfo, List<string>, Dictionary<string, string?>)>.Failure("No client executable found for the selected version.");
        }

        var sessionValid = await _accountAuth.ValidateSessionAsync(cancellationToken).ConfigureAwait(false);
        if (!sessionValid.IsSuccess)
        {
            return Result<(GameInstallInfo, List<string>, Dictionary<string, string?>)>.Failure(sessionValid.Error ?? "Sign in with your Vintage Story game account in Settings.");
        }

        var runtimeMajor = GameDotNetRuntimeRequirements.TryGetRequiredMajor(request.Version);
        if (!runtimeMajor.IsSuccess)
        {
            return Result<(GameInstallInfo, List<string>, Dictionary<string, string?>)>.Failure(runtimeMajor.Error ?? "Unsupported game version for .NET runtime.");
        }

        var runtime = await _runtimeProvisioner.EnsureAsync(
            runtimeMajor.Value,
            request.Progress,
            cancellationToken).ConfigureAwait(false);
        if (!runtime.IsSuccess)
        {
            return Result<(GameInstallInfo, List<string>, Dictionary<string, string?>)>.Failure(runtime.Error ?? "Could not provision the required .NET runtime.");
        }

        var dataPath = string.IsNullOrWhiteSpace(request.DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : request.DataPath.Trim();
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(GameInstallLayout.GetModsDirectory(dataPath));

        var applySession = await _sessionWriter.ApplySessionAsync(dataPath, cancellationToken).ConfigureAwait(false);
        if (!applySession.IsSuccess)
        {
            return Result<(GameInstallInfo, List<string>, Dictionary<string, string?>)>.Failure(applySession.Error ?? "Could not write the game session before launch.");
        }

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (runtime.Value!.IsManagedByRelic)
        {
            environment["DOTNET_ROOT"] = runtime.Value.DotNetRoot;
        }

        return Result<(GameInstallInfo, List<string>, Dictionary<string, string?>)>.Success((
            info,
            BuildLaunchArguments(dataPath, request),
            environment));
    }

    private static List<string> BuildLaunchArguments(string dataPath, GameLaunchRequest request)
    {
        var args = new List<string> { "--dataPath", dataPath };
        if (!string.IsNullOrWhiteSpace(request.ConnectAddress))
        {
            args.Add("--connect");
            args.Add(request.ConnectAddress.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.ConnectPassword))
        {
            args.Add("--pw");
            args.Add(request.ConnectPassword);
        }

        return args;
    }
}
