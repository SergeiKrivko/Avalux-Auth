namespace AvaluxAuth.Api.Schemas;

public class AccountCredentialsSchema
{
    public required string AccessToken { get; init; }
    public DateTime ExpiresAt { get; init; }
}