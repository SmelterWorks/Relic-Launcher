using System.Security.Cryptography;
using System.Text;

namespace RelicLauncher.Infrastructure.Security;

internal static class LegacyV1Crypto
{
    public static byte[] Encrypt(byte[] data)
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

    public static byte[] Decrypt(byte[] data)
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
