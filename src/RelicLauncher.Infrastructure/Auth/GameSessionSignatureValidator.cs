using System.Security.Cryptography;
using System.Text;

namespace RelicLauncher.Infrastructure.Auth;

/// <summary>
/// Verifies a cached session key against the same RSA public key the Vintage Story
/// client embeds for local session checks (see SessionManager.IsCachedSessionKeyValid).
/// This is a local sanity check only, not a substitute for the clientvalidate call.
/// </summary>
internal sealed class GameSessionSignatureValidator : ISessionSignatureValidator
{
    private const string ModulusBase64 =
        "mRaP5hO0mWf6gIdPMFD0sg4KLhwsA08Tk2246fdwNk6G7cRk+BJYtTOwKO+plurICQMKF2ktDJWOkjz+Hq2BCjBDB/al7XNdnoOJ1w0BsgInEPOGz9nn8OM4GjQyNcuv0iY0XqwElgy5xCNjBRKJJuqQje/E5SIiHs2O78nJUsZWCv6xjaH+4N/3Kno+sQoBFpNqKmXsq1+2KGMu8t4x58LrojbXzxJUm3O3agK8MvDg/xTAmumd2PTjVJBnrlSBIPdsaQwzX1G9s29B7CzQC6T7TzQehA8hPmUSQLEnwBV6EaUXbcjOBh01i5k5MP6i22wrDCfQMnnkch+i+UsgyQ==";

    private const string ExponentBase64 = "AQAB";

    public bool IsValid(string? sessionKey, string? sessionSignature, string? playerUid)
    {
        if (string.IsNullOrEmpty(sessionKey) ||
            string.IsNullOrEmpty(sessionSignature) ||
            string.IsNullOrEmpty(playerUid))
        {
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Convert.FromBase64String(ModulusBase64),
                Exponent = Convert.FromBase64String(ExponentBase64),
            });

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sessionKey));
            var signature = Convert.FromBase64String(sessionSignature);
            return rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
