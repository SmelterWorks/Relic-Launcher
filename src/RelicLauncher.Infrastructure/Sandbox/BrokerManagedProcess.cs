using System.Text;

namespace RelicLauncher.Infrastructure.Sandbox;

internal sealed class BrokerManagedProcess : IDisposable
{
    private readonly global::System.Diagnostics.Process _process;
    private readonly MemoryStream _stdoutBuffer = new();
    private readonly MemoryStream _stderrBuffer = new();
    private readonly Lock _gate = new();

    public BrokerManagedProcess(global::System.Diagnostics.Process process)
    {
        _process = process;
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            lock (_gate)
            {
                var bytes = Encoding.UTF8.GetBytes(e.Data + Environment.NewLine);
                _stdoutBuffer.Write(bytes, 0, bytes.Length);
            }
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            lock (_gate)
            {
                var bytes = Encoding.UTF8.GetBytes(e.Data + Environment.NewLine);
                _stderrBuffer.Write(bytes, 0, bytes.Length);
            }
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public byte[] ReadOutput()
    {
        lock (_gate)
        {
            var stdout = _stdoutBuffer.ToArray();
            var stderr = _stderrBuffer.ToArray();
            _stdoutBuffer.SetLength(0);
            _stderrBuffer.SetLength(0);
            if (stderr.Length == 0)
            {
                return stdout;
            }

            if (stdout.Length == 0)
            {
                return stderr;
            }

            var combined = new byte[stdout.Length + stderr.Length];
            Buffer.BlockCopy(stdout, 0, combined, 0, stdout.Length);
            Buffer.BlockCopy(stderr, 0, combined, stdout.Length, stderr.Length);
            return combined;
        }
    }

    public void WriteInput(byte[] data)
    {
        _process.StandardInput.BaseStream.Write(data, 0, data.Length);
        _process.StandardInput.Flush();
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort.
        }

        _process.Dispose();
    }
}
