using System.Text;

namespace AvaluxAuth.Utils;

/// <summary>
/// Prepares untrusted text for logging (M-05 in SECURITY-AUDIT.md).
/// </summary>
/// <remarks>
/// Values that reach the log from outside (a provider's <c>error</c>/<c>error_description</c>,
/// a provider response body, an exception message built from them) are attacker-controlled.
/// Logging them verbatim allows log forging (a <c>\n</c> produces a fake entry) and log
/// flooding (an unbounded value). Control characters are therefore replaced with spaces and
/// the result is truncated to a bounded length.
/// </remarks>
public static class LogSanitizer
{
    public const int DefaultMaxLength = 512;

    public static string Sanitize(string? value, int maxLength = DefaultMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 16);

        if (string.IsNullOrEmpty(value))
            return "[empty]";

        var truncated = value.Length > maxLength;
        var take = truncated ? maxLength - 1 : value.Length;
        var builder = new StringBuilder(take + 1);
        for (var i = 0; i < take; i++)
            builder.Append(char.IsControl(value[i]) ? ' ' : value[i]);

        if (truncated)
            builder.Append('…');

        var sanitized = builder.ToString().Trim();
        return sanitized.Length == 0 ? "[blank]" : sanitized;
    }
}
