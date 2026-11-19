using AvaluxAuth.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SixLabors.ImageSharp;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/avatar")]
public class AvatarController(
    IImageService imageService,
    IFileRepository fileRepository,
    IStateRepository stateRepository,
    ILogger<AvatarController> logger) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatar([FromQuery] string text = "",
        [FromQuery(Name = "color")] int? colorIndex = null,
        CancellationToken ct = default)
    {
        var stream = await imageService.GenerateAvatar(text, colorIndex, ct);
        return File(stream, "image/png");
    }

    /// <summary>
    /// M-01 in SECURITY-AUDIT.md: uploads are not anonymous. A request must carry
    /// the <c>state</c> of a live authorization flow (the auth UI uploads the
    /// avatar during sign-up/profile editing), and each state may upload only
    /// <see cref="Config.MaxAvatarUploadsPerState"/> times. Together with the
    /// per-IP rate limit and the orphan cleanup this bounds the bucket as a free
    /// image host. Files that are never attached to an account are removed by
    /// <see cref="AvatarCleanupService"/>.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [EnableCors(PolicyName = Config.AdminPolicy)]
    [EnableRateLimiting(Config.AvatarUploadRateLimitPolicy)]
    public async Task<ActionResult<Guid>> UploadAvatar([FromQuery] string? state, IFormFile file,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(state) || await stateRepository.GetStateAsync(state) == null)
            return Unauthorized();

        // The attempt is counted before decoding, so a malformed image cannot be
        // used to bypass the per-state quota.
        if (!await stateRepository.TryRegisterUploadAsync(state, Config.MaxAvatarUploadsPerState))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                "Too many avatar uploads for this session.");

        if (file.Length == 0)
            return BadRequest("Empty file");

        var id = Guid.NewGuid();
        try
        {
            await using var notPngStream = file.OpenReadStream();
            await using var pngStream = await imageService.ConvertToPng(notPngStream, ct);
            await fileRepository.UploadFileAsync(FileRepositoryBucket.Avatars, id, "avatar.png", pngStream, ct);
        }
        catch (Exception e) when (e is UnknownImageFormatException or InvalidImageContentException)
        {
            // A non-image or a truncated image is a client error, not a 500.
            logger.LogWarning("Rejected avatar upload: {Type}", e.GetType().Name);
            return BadRequest("Unsupported image");
        }

        logger.LogInformation("Avatar uploaded: avatarId={AvatarId}, ip={ClientIp}", id,
            HttpContext.GetClientIpAddress());
        return Ok(id);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadFromS3(Guid id, CancellationToken ct = default)
    {
        var stream = await fileRepository.DownloadFileAsync(FileRepositoryBucket.Avatars, id, "avatar.png", ct);
        return File(stream, "image/png");
    }
}