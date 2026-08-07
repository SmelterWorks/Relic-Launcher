using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Sandbox;

internal sealed class BrokerPipeTransport : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Stream _stream;
    private readonly ILogger _logger;

    public BrokerPipeTransport(Stream stream, ILogger logger)
    {
        _stream = stream;
        _logger = logger;
    }

    public async Task<BrokerResponse> SendAsync(BrokerRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);
        var length = BitConverter.GetBytes(payload.Length);
        await _stream.WriteAsync(length, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var lengthBuf = new byte[4];
        await ReadExactAsync(_stream, lengthBuf, cancellationToken).ConfigureAwait(false);
        var responseLength = BitConverter.ToInt32(lengthBuf, 0);
        if (responseLength <= 0 || responseLength > 16 * 1024 * 1024)
        {
            return new BrokerResponse { Ok = false, Error = "Invalid broker response length." };
        }

        var responseBuf = new byte[responseLength];
        await ReadExactAsync(_stream, responseBuf, cancellationToken).ConfigureAwait(false);
        var responseJson = Encoding.UTF8.GetString(responseBuf);
        return JsonSerializer.Deserialize<BrokerResponse>(responseJson, JsonOptions)
            ?? new BrokerResponse { Ok = false, Error = "Broker response was empty." };
    }

    public static async Task<BrokerPipeTransport> ConnectNamedPipeAsync(string pipeName, CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, cancellationToken).ConfigureAwait(false);
        return new BrokerPipeTransport(pipe, NullLogger.Instance);
    }

    public static async Task<BrokerPipeTransport> ConnectUnixSocketAsync(string socketPath, CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var endpoint = new UnixDomainSocketEndPoint(socketPath);
        await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        var stream = new NetworkStream(socket, ownsSocket: true);
        return new BrokerPipeTransport(stream, NullLogger.Instance);
    }

    public static BrokerPipeTransport FromStream(Stream stream, ILogger logger) =>
        new(stream, logger);

    public async ValueTask DisposeAsync()
    {
        if (_stream is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _stream.Dispose();
        }
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Broker connection closed.");
            }

            offset += read;
        }
    }

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
