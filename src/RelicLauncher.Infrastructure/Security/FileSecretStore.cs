using System.Security.Cryptography;
using System.Text;
using RelicLauncher.Core.Abstractions;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Infrastructure.Security;

public sealed class FileSecretStore : ISecretStore
{
    private readonly string _directory;

    public FileSecretStore(IAppPathProvider pathProvider)
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
            var protectedBytes = Encrypt(plain);
            await File.WriteAllBytesAsync(path, protectedBytes, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
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
            var plain = Decrypt(protectedBytes);
            return Result<string?>.Success(Encoding.UTF8.GetString(plain));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
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

    private static byte[] Encrypt(byte[] data)
    {
        var key = DeriveKey();
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

    private static byte[] Decrypt(byte[] data)
    {
        if (data.Length < 28)
        {
            throw new CryptographicException("Secret payload is too short.");
        }

        var key = DeriveKey();
        var nonce = data.AsSpan(0, 12);
        var tag = data.AsSpan(12, 16);
        var cipher = data.AsSpan(28);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private static byte[] DeriveKey()
    {
        var material = Encoding.UTF8.GetBytes(
            Environment.UserName + "|" +
            Environment.MachineName + "|" +
            "RelicLauncher.Secrets.v1");
        return SHA256.HashData(material);
    }
}
