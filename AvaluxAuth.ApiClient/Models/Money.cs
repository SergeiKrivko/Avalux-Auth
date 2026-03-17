namespace Avalux.Auth.ApiClient.Models;

public class Money
{
    public required string Currency { get; init; }
    public required decimal Amount { get; init; }
}