namespace AvaluxAuth.Api.Schemas;

public class ClientInfoResponse
{
    public string? Name { get; init; }

    /// <summary>
    /// URL of the application icon, so the sign-in page can show the client's
    /// own branding instead of the generic Avalux Auth mark.
    /// </summary>
    public string? IconUrl { get; init; }
}