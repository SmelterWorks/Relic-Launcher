namespace RelicLauncher.Infrastructure.Auth;

internal interface ISessionSignatureValidator
{
    bool IsValid(string? sessionKey, string? sessionSignature, string? playerUid);
}
