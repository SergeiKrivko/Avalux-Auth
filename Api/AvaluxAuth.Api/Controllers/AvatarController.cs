using AvaluxAuth.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/avatar")]
public class AvatarController(IImageService imageService, IFileRepository fileRepository) : ControllerBase
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

    [HttpPost]
    [RequestSizeLimit(64 * 1024)]
    [EnableCors(PolicyName = Config.AdminPolicy)]
    public async Task<ActionResult<Guid>> UploadAvatar(IFormFile file, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        await using var stream = file.OpenReadStream();
        await fileRepository.UploadFileAsync(FileRepositoryBucket.Avatars, id, "avatar.png", stream, ct);
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