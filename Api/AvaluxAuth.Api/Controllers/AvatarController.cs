using AvaluxAuth.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AvaluxAuth.Api.Controllers;

[ApiController]
[Route("api/v1/avatar")]
public class AvatarController(IImageService imageService) : ControllerBase
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
}