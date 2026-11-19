using AvaluxAuth.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

/// <summary>
/// Public read-only access to application icons. The icon is shown on the
/// sign-in page of the user-facing auth UI and in the admin panel, so it cannot
/// require a session; the application id is a random GUID and the response is
/// only an image.
/// </summary>
[ApiController]
[Route("api/v1/apps")]
public class ApplicationIconsController(IFileRepository fileRepository) : ControllerBase
{
    /// <summary>Object key inside <see cref="FileRepositoryBucket.Icons"/>.</summary>
    private const string IconFileName = "icon.png";

    [HttpGet("{applicationId:guid}/icon")]
    [AllowAnonymous]
    public async Task<IActionResult> GetIcon(Guid applicationId, CancellationToken ct = default)
    {
        if (!await fileRepository.FileExistsAsync(FileRepositoryBucket.Icons, applicationId, IconFileName, ct))
            return NotFound();

        var stream = await fileRepository.DownloadFileAsync(FileRepositoryBucket.Icons, applicationId, IconFileName,
            ct);

        // The URL carries a version query set on every upload, so a replaced icon
        // is fetched anew while the object itself can be cached for a long time.
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(stream, "image/png");
    }
}
