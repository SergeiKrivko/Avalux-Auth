using AvaluxAuth.Models;

namespace AvaluxAuth.Api.Schemas;

/// <summary>
/// Profile of a password account. R-02 in SECURITY-AUDIT.md adds the
/// confirmation flag, which is not part of what clients may send back on
/// sign-up or profile update.
/// </summary>
public class PasswordProfileSchema : PasswordUserInfo
{
    public required bool EmailConfirmed { get; init; }
}
