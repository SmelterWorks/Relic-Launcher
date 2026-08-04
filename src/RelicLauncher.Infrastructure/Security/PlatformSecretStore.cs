using System.Security.Cryptography;
using System.Text;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Security;

public sealed class PlatformSecretStore : ISecretStore
{
    private static readonly byte[] MagicV2 = "RLS2"u8.ToArray();
    private readonly string _directory;
    private readonly Lock _masterKeyGate = new();
    private byte[]? _masterKey;

    public PlatformSecretStore(IAppPathProvider pathProvider)
    {
        _directory = pathProvider.GetPaths().SecretsDirectory;
    }

    public async Task<Result> SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var path = GetPath(key);
            var plain = Encoding.UTF8.GetBytes(value);
            var protectedBytes = Protect(plain);
            await File.WriteAllBytesAsync(path, protectedBytes, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or PlatformNotSupportedException)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result<string?>> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = GetPath(key);
            if (!File.Exists(path))
            {
                return Result<string?>.Success(null);
            }

            var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            byte[] plain;
            if (IsV2(protectedBytes))
            {
                plain = Unprotect(protectedBytes);
            }
            else
            {
                plain = LegacyV1Crypto.Decrypt(protectedBytes);
                var migrated = Protect(plain);
                await File.WriteAllBytesAsync(path, migrated, cancellationToken).ConfigureAwait(false);
            }

            return Result<string?>.Success(Encoding.UTF8.GetString(plain));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or PlatformNotSupportedException)
        {
            return Result<string?>.Failure(ex.Message);
        }
    }

    public Task<Result> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = GetPath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result.Failure(ex.Message));
        }
    }

    private string GetPath(string key)
    {
        var safe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32].ToLowerInvariant();
        return Path.Combine(_directory, safe + ".bin");
    }

    private static bool IsV2(byte[] data)
        => data.Length >= MagicV2.Length && data.AsSpan(0, MagicV2.Length).SequenceEqual(MagicV2);

    private byte[] Protect(byte[] plain)
    {
        var payload = OperatingSystem.IsWindows()
            ? ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser)
            : EncryptWithMasterKey(plain);

        var result = new byte[MagicV2.Length + payload.Length];
        Buffer.BlockCopy(MagicV2, 0, result, 0, MagicV2.Length);
        Buffer.BlockCopy(payload, 0, result, MagicV2.Length, payload.Length);
        return result;
    }

    private byte[] Unprotect(byte[] data)
    {
        var payload = data.AsSpan(MagicV2.Length).ToArray();
        return OperatingSystem.IsWindows()
            ? ProtectedData.Unprotect(payload, optionalEntropy: null, DataProtectionScope.CurrentUser)
            : DecryptWithMasterKey(payload);
    }

    private byte[] EncryptWithMasterKey(byte[] data)
    {
        var key = GetOrCreateMasterKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, data, cipher, tag);
        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);
        return result;
    }

    private byte[] DecryptWithMasterKey(byte[] data)
    {
        if (data.Length < 28)
        {
            throw new CryptographicException("Secret payload is too short.");
        }

        var key = GetOrCreateMasterKey();
        var nonce = data.AsSpan(0, 12);
        var tag = data.AsSpan(12, 16);
        var cipher = data.AsSpan(28);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private byte[] GetOrCreateMasterKey()
    {
        lock (_masterKeyGate)
        {
            if (_masterKey is not null)
            {
                return _masterKey;
            }

            if (TryLoadMasterKeyFromOs(out var fromOs))
            {
                _masterKey = fromOs;
                return _masterKey;
            }

            var path = Path.Combine(_directory, ".master.key");
            if (File.Exists(path))
            {
                _masterKey = File.ReadAllBytes(path);
                return _masterKey;
            }

            Directory.CreateDirectory(_directory);
            _masterKey = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(path, _masterKey);
            TryRestrictFileAccess(path);
            TryPersistMasterKeyToOs(_masterKey);
            return _masterKey;
        }
    }

    private static bool TryLoadMasterKeyFromOs(out byte[] key)
    {
        key = [];
        if (OperatingSystem.IsMacOS())
        {
            return TryRunSecretCommand(
                ["security", "find-generic-password", "-s", "RelicLauncher", "-a", "master", "-w"],
                out var hex) && TryParseHexKey(hex, out key);
        }

        if (OperatingSystem.IsLinux())
        {
            return TryRunSecretCommand(
                ["secret-tool", "lookup", "service", "RelicLauncher", "account", "master"],
                out var hex) && TryParseHexKey(hex, out key);
        }

        return false;
    }

    private static void TryPersistMasterKeyToOs(byte[] key)
    {
        var hex = Convert.ToHexString(key);
        if (OperatingSystem.IsMacOS())
        {
            TryRunSecretCommand(
                ["security", "add-generic-password", "-U", "-s", "RelicLauncher", "-a", "master", "-w", hex],
                out _);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                using var process = global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
                {
                    FileName = "secret-tool",
                    ArgumentList = { "store", "--label=RelicLauncher", "service", "RelicLauncher", "account", "master" },
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (process is null)
                {
                    return;
                }

                process.StandardInput.Write(hex);
                process.StandardInput.Close();
                process.WaitForExit(3000);
            }
            catch
            {
                // File fallback remains.
            }
        }
    }

    private static bool TryRunSecretCommand(string[] argv, out string output)
    {
        output = string.Empty;
        try
        {
            var psi = new global::System.Diagnostics.ProcessStartInfo
            {
                FileName = argv[0],
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            for (var i = 1; i < argv.Length; i++)
            {
                psi.ArgumentList.Add(argv[i]);
            }

            using var process = global::System.Diagnostics.Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                return false;
            }

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseHexKey(string hex, out byte[] key)
    {
        key = [];
        try
        {
            key = Convert.FromHexString(hex.Trim());
            return key.Length == 32;
        }
        catch
        {
            return false;
        }
    }

    private static void TryRestrictFileAccess(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best effort.
        }
    }
}
