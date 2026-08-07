using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;
using RelicLauncher.Core.Versions;

namespace RelicLauncher.Infrastructure.Launch;

public sealed class GameLaunchService : IGameLaunchService
{
    private readonly IProcessRunner _processRunner;
    private readonly IRuntimePlatform _platform;
    private readonly IClientSettingsSessionWriter _sessionWriter;
    private readonly IDotNetRuntimeProvisioner _runtimeProvisioner;
    private readonly IAccountAuthService _accountAuth;

    public GameLaunchService(
        IProcessRunner processRunner,
        IRuntimePlatform platform,
        IClientSettingsSessionWriter sessionWriter,
        IDotNetRuntimeProvisioner runtimeProvisioner,
        IAccountAuthService accountAuth)
    {
        _processRunner = processRunner;
        _platform = platform;
        _sessionWriter = sessionWriter;
        _runtimeProvisioner = runtimeProvisioner;
        _accountAuth = accountAuth;
    }

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
        var resolved = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        if (!resolved.IsSuccess)
        {
            return Result.Failure(resolved.Error ?? "Could not resolve install.");
        }

        var info = resolved.Value!;
        if (!info.ExecutableFound || string.IsNullOrWhiteSpace(info.ExecutablePath))
        {
            return Result.Failure("No client executable found for the selected version.");
        }

        var sessionValid = await _accountAuth.ValidateSessionAsync(cancellationToken).ConfigureAwait(false);
        if (!sessionValid.IsSuccess)
        {
            return Result.Failure(sessionValid.Error ?? "Sign in with your Vintage Story game account in Settings.");
        }

        var runtimeMajor = GameDotNetRuntimeRequirements.TryGetRequiredMajor(request.Version);
        if (!runtimeMajor.IsSuccess)
        {
            return Result.Failure(runtimeMajor.Error ?? "Unsupported game version for .NET runtime.");
        }

        var runtime = await _runtimeProvisioner.EnsureAsync(
            runtimeMajor.Value,
            request.Progress,
            cancellationToken).ConfigureAwait(false);
        if (!runtime.IsSuccess)
        {
            return Result.Failure(runtime.Error ?? "Could not provision the required .NET runtime.");
        }

        var dataPath = string.IsNullOrWhiteSpace(request.DataPath)
            ? _platform.GetPlatformInfo().DefaultDataPath
            : request.DataPath.Trim();
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(GameInstallLayout.GetModsDirectory(dataPath));

        var applySession = await _sessionWriter.ApplySessionAsync(dataPath, cancellationToken).ConfigureAwait(false);
        if (!applySession.IsSuccess)
        {
            return Result.Failure(applySession.Error ?? "Could not write the game session before launch.");
        }

        var args = BuildLaunchArguments(dataPath, request);
        IReadOnlyDictionary<string, string?>? environment = null;
        if (runtime.Value!.IsManagedByRelic)
        {
            environment = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["DOTNET_ROOT"] = runtime.Value.DotNetRoot,
            };
        }

        return await _processRunner.StartAsync(info.ExecutablePath, args, environment, cancellationToken)
            .ConfigureAwait(false);
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
