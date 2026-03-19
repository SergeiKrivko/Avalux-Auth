using AvaluxAuth.Models;

namespace AvaluxAuth.Api.Schemas;

public class PasswordSignInSchema
{
    public required string Login { get; init; }
    public required string Password { get; init; }
}

public class PasswordSignUpSchema : PasswordSignInSchema
{
    public required UserInfo UserInfo { get; init; }
}