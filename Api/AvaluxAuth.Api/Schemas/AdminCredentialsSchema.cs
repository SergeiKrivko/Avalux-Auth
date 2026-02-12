namespace AvaluxAuth.Api.Schemas;

public class AdminCredentialsSchema
{
    public required string Login { get; init; }
    public required string Password { get; init; }
}