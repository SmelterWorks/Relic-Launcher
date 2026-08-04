using Serilog;
using Serilog.Events;

namespace RelicLauncher.Infrastructure.Logging;

public static class SerilogBootstrap
{
    public static ILogger CreateLogger(string logsDirectory)
    {
        var logPath = Path.Combine(logsDirectory, "relic-.log");

        var config = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}");

#if DEBUG
        config = config.WriteTo.Debug();
#endif

        return config.CreateLogger();
    }
}
