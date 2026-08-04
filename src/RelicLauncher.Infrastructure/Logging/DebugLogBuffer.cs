using System.Collections.Concurrent;
using System.Globalization;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Models;
using Serilog.Core;
using Serilog.Events;

namespace RelicLauncher.Infrastructure.Logging;

public sealed class DebugLogBuffer : IDebugLogBuffer, ILogEventSink
{
    private const int Capacity = 300;
    private readonly ConcurrentQueue<DebugLogEntry> _entries = new();
    private int _count;

    public event EventHandler? Changed;

    public IReadOnlyList<DebugLogEntry> GetEntries()
        => _entries.Reverse().ToList();

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref _count, 0);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Warning)
        {
            return;
        }

        var entry = new DebugLogEntry
        {
            Timestamp = logEvent.Timestamp.ToUniversalTime(),
            Level = logEvent.Level.ToString().ToUpper(CultureInfo.InvariantCulture),
            Message = logEvent.RenderMessage(),
            Source = logEvent.Properties.TryGetValue("SourceContext", out var source)
                ? Convert.ToString(source, CultureInfo.InvariantCulture)?.Trim('"')
                : null,
            Exception = logEvent.Exception?.ToString(),
        };

        _entries.Enqueue(entry);
        if (Interlocked.Increment(ref _count) > Capacity)
        {
            if (_entries.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
