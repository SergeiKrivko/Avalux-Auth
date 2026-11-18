using System.Security.Cryptography;
using System.Text;

namespace AvaluxAuth.Utils;

/// <summary>
/// Helpers for Proof Key for Code Exchange (RFC 7636). Only the <c>S256</c> method is supported:
/// the <c>plain</c> method does not protect a code intercepted in transit and is rejected.
/// </summary>
public static class Pkce
{
    public const string S256 = "S256";

    private const int MinVerifierLength = 43;
    private const int MaxVerifierLength = 128;

    /// <summary>
    /// A valid S256 challenge is the unpadded base64url encoding of a SHA-256 hash,
    /// which is always exactly 43 characters long.
    /// </summary>
    public static bool IsValidChallenge(string? challenge)
    {
        return challenge is { Length: 43 } && challenge.All(IsBase64UrlChar);
    }

    /// <summary>Code verifier must be 43-128 characters from the RFC 7636 unreserved set.</summary>
    public static bool IsValidVerifier(string? verifier)
    {
        return verifier is { Length: >= MinVerifierLength and <= MaxVerifierLength } && verifier.All(IsUnreservedChar);
    }

    /// <summary>
    /// Constant-time check that <paramref name="verifier"/> hashes to <paramref name="challenge"/>.
    /// </summary>
    public static bool Verify(string? verifier, string? challenge, string? method)
    {
        if (!string.Equals(method, S256, StringComparison.Ordinal))
            return false;
        if (!IsValidVerifier(verifier) || !IsValidChallenge(challenge))
            return false;

        var computed = ComputeChallenge(verifier!);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(challenge!));
    }

    public static string ComputeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    public static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool IsBase64UrlChar(char c)
    {
        return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
    }

    private static bool IsUnreservedChar(char c)
    {
        return IsBase64UrlChar(c) || c == '.' || c == '~';
    }
}
