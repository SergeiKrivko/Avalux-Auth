using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IApplicationService
{
    public Task<Guid> CreateApplicationAsync(ApplicationParameters parameters, CancellationToken ct = default);
    public Task<string> RecreateClientSecretAsync(Guid applicationId, CancellationToken ct = default);

    /// <summary>
    /// Validates, converts and stores the application icon in S3 and returns its
    /// public URL. Returns <c>null</c> when the application does not exist.
    /// Throws <see cref="SixLabors.ImageSharp.UnknownImageFormatException"/> for
    /// input that is not an image.
    /// </summary>
    public Task<string?> SetIconAsync(Guid applicationId, Stream file, CancellationToken ct = default);

    /// <summary>
    /// Clears the icon URL and deletes the object from S3. Returns <c>false</c>
    /// when the application does not exist.
    /// </summary>
    public Task<bool> DeleteIconAsync(Guid applicationId, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes the application and removes its icon from S3.
    /// </summary>
    public Task<bool> DeleteApplicationAsync(Guid applicationId, CancellationToken ct = default);
}
