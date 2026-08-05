using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Paths;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Process;

public sealed class SafeProcessRunner : IProcessRunner
{
    private readonly ILogger<SafeProcessRunner> _logger;

    public SafeProcessRunner(ILogger<SafeProcessRunner> logger)
    {
        _logger = logger;
    }

    public Task<Result> StartAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
        => StartAsync(executablePath, arguments, environment: null, cancellationToken);

    public Task<Result> StartAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = Validate(executablePath, arguments);
        if (!validation.IsSuccess)
        {
            return Task.FromResult(Result.Failure(validation.Error!));
        }

        return Task.FromResult(StartProcess(validation.Value!, arguments ?? Array.Empty<string>(), environment));
    }

    private static Result<string> Validate(string executablePath, IReadOnlyList<string>? arguments)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return Result<string>.Failure("Executable path is empty.");
        }

        if (!PathValidator.TryGetFullPath(executablePath, out var fullPath, out var pathError))
        {
            return Result<string>.Failure(pathError);
        }

        if (!File.Exists(fullPath))
        {
            return Result<string>.Failure($"Executable not found: {fullPath}");
        }

        if (arguments is not null && arguments.Any(static a => a is null))
        {
            return Result<string>.Failure("Argument list contains null.");
        }

        return Result<string>.Success(fullPath);
    }

    private Result StartProcess(
        string fullPath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment)
    {
        try
        {
            var startInfo = new global::System.Diagnostics.ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory,
            };

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            if (environment is not null)
            {
                foreach (var pair in environment)
                {
                    startInfo.Environment[pair.Key] = pair.Value;
                }
            }

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return Result.Failure("Process.Start returned null.");
            }

            _logger.LogInformation("Started process {Path} (pid {Pid})", fullPath, process.Id);
            return Result.Success();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                        or UnauthorizedAccessException
                                        or InvalidOperationException
                                        or IOException)
        {
            _logger.LogError(ex, "Failed to start {Path}", fullPath);
            return Result.Failure(ex.Message);
        }
    }
}
