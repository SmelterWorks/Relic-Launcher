namespace RelicLauncher.App.Services;

public sealed class ModInstallResult
{
    public bool Success { get; init; }
    public bool Canceled { get; init; }
    public string? Message { get; init; }

    public static ModInstallResult Ok(string? message = null)
        => new() { Success = true, Message = message };

    public static ModInstallResult Fail(string message)
        => new() { Success = false, Message = message };

    public static ModInstallResult Cancel()
        => new() { Success = false, Canceled = true, Message = "Install canceled." };
}
