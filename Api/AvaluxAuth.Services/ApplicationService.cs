using System.Globalization;
using System.Security.Cryptography;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AvaluxAuth.Services;

public class ApplicationService(
    IApplicationRepository applicationRepository,
    IFileRepository fileRepository,
    IImageService imageService,
    IConfiguration configuration,
    ILogger<ApplicationService> logger) : IApplicationService
{
    /// <summary>Object key inside <see cref="FileRepositoryBucket.Icons"/>.</summary>
    private const string IconFileName = "icon.png";

    public async Task<Guid> CreateApplicationAsync(ApplicationParameters parameters, CancellationToken ct = default)
    {
        var rn = RandomNumberGenerator.Create();
        var clientId = rn.RandomString();
        var clientSecret = rn.RandomString();
        return await applicationRepository.CreateApplicationAsync(parameters, clientId, clientSecret, ct);
    }

    public async Task<string> RecreateClientSecretAsync(Guid applicationId, CancellationToken ct = default)
    {
        var clientSecret = RandomNumberGenerator.GetRandomString();
        await applicationRepository.UpdateClientSecretAsync(applicationId, clientSecret, ct);
        return clientSecret;
    }

    public async Task<string?> SetIconAsync(Guid applicationId, Stream file, CancellationToken ct = default)
    {
        var application = await applicationRepository.GetApplicationByIdAsync(applicationId, ct);
        if (application is null)
            return null;

        // The object key is the application id, so a replacement overwrites the
        // previous icon instead of leaving an unreferenced object in the bucket.
        await using var png = await imageService.ConvertIconToPng(file, ct);
        await fileRepository.UploadFileAsync(FileRepositoryBucket.Icons, applicationId, IconFileName, png, ct);

        var iconUrl = BuildIconUrl(applicationId);
        await applicationRepository.UpdateApplicationIconAsync(applicationId, iconUrl, ct);
        logger.LogInformation("Application icon updated: applicationId={ApplicationId}", applicationId);
        return iconUrl;
    }

    public async Task<bool> DeleteIconAsync(Guid applicationId, CancellationToken ct = default)
    {
        // The URL is cleared first: if S3 is unavailable the worst case is an
        // unreferenced object, never an application pointing at a missing icon.
        if (!await applicationRepository.UpdateApplicationIconAsync(applicationId, null, ct))
            return false;

        await TryDeleteIconFileAsync(applicationId, ct);
        logger.LogInformation("Application icon removed: applicationId={ApplicationId}", applicationId);
        return true;
    }

    public async Task<bool> DeleteApplicationAsync(Guid applicationId, CancellationToken ct = default)
    {
        if (!await applicationRepository.DeleteApplicationAsync(applicationId, ct))
            return false;

        await TryDeleteIconFileAsync(applicationId, ct);
        return true;
    }

    private async Task TryDeleteIconFileAsync(Guid applicationId, CancellationToken ct)
    {
        try
        {
            await fileRepository.DeleteFileAsync(FileRepositoryBucket.Icons, applicationId, IconFileName, ct);
        }
        catch (Exception e)
        {
            // A soft-deleted application must not fail because of leftover S3
            // state; the object is inert and bounded by one per application.
            logger.LogWarning(e, "Failed to delete the icon object for applicationId={ApplicationId}", applicationId);
        }
    }

    private string BuildIconUrl(Guid applicationId)
    {
        var apiUrl = (configuration["Api:ApiUrl"] ?? string.Empty).TrimEnd('/');

        // The version makes a replaced icon bypass the browser cache: the URL
        // changes on every upload while the object key stays the same.
        return new UrlBuilder($"{apiUrl}/api/v1/apps/{applicationId}/icon")
            .AddQuery("v", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
            .ToString();
    }
}
