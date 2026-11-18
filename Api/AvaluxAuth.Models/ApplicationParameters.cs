namespace AvaluxAuth.Models;

public class ApplicationParameters
{
    public required string Name { get; init; }
    public string[] RedirectUrls { get; init; } = [];

    /// <summary>
    /// Confidential clients authenticate at the token endpoint with their client secret.
    /// Public clients (SPAs, desktop apps, anything with a loopback redirect URI) cannot keep
    /// a secret and are protected by mandatory PKCE instead (H-05).
    /// </summary>
    public bool RequireClientSecret { get; init; } = true;
}