namespace AvaluxAuth.Models;

public class Application
{
    public required Guid Id { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required ApplicationParameters Parameters { get; init; }

    /// <summary>
    /// Absolute URL of the application icon served from S3
    /// (<c>{Api:ApiUrl}/api/v1/apps/{id}/icon</c>), or <c>null</c> when the
    /// application has no icon. The icon itself is uploaded on the application
    /// configuration page and lives in the <c>S3:Buckets:Icons</c> bucket.
    /// </summary>
    public string? IconUrl { get; init; }

    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
}