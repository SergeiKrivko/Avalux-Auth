namespace AvaluxAuth.Api.Schemas;

/// <summary>
/// R-02 in SECURITY-AUDIT.md: present in the sign-up answer only when a
/// confirmation letter was actually queued. The auth UI uses it to show the
/// "check your e-mail" page, whose skip action is the redirect URL returned next
/// to it.
/// </summary>
public class EmailConfirmationSchema
{
    /// <summary>The address the letter was sent to, as stored on the account.</summary>
    public required string Email { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public class EmailConfirmationCompleteSchema
{
    /// <summary>One-time token from the link; accepted only in the POST body.</summary>
    public required string Token { get; init; }
}

public class EmailConfirmationResponseSchema
{
    /// <summary>
    /// Set when the link also finished the interrupted authorization: the client
    /// redirect URI with a fresh code (and the original OAuth state). Null for a
    /// link that only confirms a changed address, where the user is already
    /// signed in.
    /// </summary>
    public string? RedirectUrl { get; init; }
}
