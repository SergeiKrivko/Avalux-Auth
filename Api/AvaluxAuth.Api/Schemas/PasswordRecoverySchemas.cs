namespace AvaluxAuth.Api.Schemas;

public class PasswordRecoverSchema
{
    /// <summary>Login or the e-mail address stored on the account.</summary>
    public required string LoginOrEmail { get; init; }
}

public class PasswordResetCompleteSchema
{
    public required string Token { get; init; }
    public required string Password { get; init; }
}
