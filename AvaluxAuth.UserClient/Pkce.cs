using System.Security.Cryptography;
using System.Text;

namespace Avalux.Auth.UserClient;

/// <summary>
/// PKCE helpers (RFC 7636, S256) and the anti-CSRF <c>state</c> generator used by the
/// installed-app flow. Avalux Auth requires PKCE for public clients, so the verifier never
/// leaves the process: only its SHA-256 challenge is sent in the browser URL.
/// </summary>
internal static class Pkce
{
    public static string CreateCodeVerifier()
    {
        // 32 random bytes -> 43 base64url characters, the RFC 7636 minimum.
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    public static string CreateCodeChallenge(string verifier)
    {
        return Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    }

    public static string CreateState()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
