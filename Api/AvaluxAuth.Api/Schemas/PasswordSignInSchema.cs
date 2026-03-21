using AvaluxAuth.Models;

namespace AvaluxAuth.Api.Schemas;

public class PasswordSignInSchema
{
    public required string Login { get; init; }
    public required string Password { get; init; }
}

public class PasswordSignUpSchema : PasswordSignInSchema
{
    public required PasswordUserInfo UserInfo { get; init; }
}

public class UpdateProfileSchema
{
    public string? OldPassword { get; init; }
    public string? NewPassword { get; init; }
    public required PasswordUserInfo UserInfo { get; init; }
}