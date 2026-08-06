using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using RelicLauncher.Infrastructure.Security;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class LegacyV1CryptoTests
{
    [Fact]
    public void EncryptDecrypt_RoundTripsPlaintext()
    {
        var plain = Encoding.UTF8.GetBytes("launcher-secret-value");
        var encrypted = LegacyV1Crypto.Encrypt(plain);
        var decrypted = LegacyV1Crypto.Decrypt(encrypted);

        decrypted.Should().Equal(plain);
        encrypted.Should().NotEqual(plain);
    }

    [Fact]
    public void Decrypt_ShortPayload_ThrowsCryptographicException()
    {
        var act = () => LegacyV1Crypto.Decrypt(new byte[10]);
        act.Should().Throw<CryptographicException>();
    }
}
