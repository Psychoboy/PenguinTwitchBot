using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Bot.Overlay.Credits;

namespace PenguinTwitchBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Streamer, Editor")]
    public sealed class CreditsController(
        ICreditsService creditsService,
        IOverlaySecurityService overlaySecurityService) : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet("render")]
        public async Task<IActionResult> Render(
            [FromQuery] string? profile = null,
            [FromQuery] string? sample = null,
            [FromQuery] string? windowMode = null,
            [FromQuery] int? lookbackMinutes = null,
            CancellationToken cancellationToken = default)
        {
            var payload = await creditsService.RenderAsync(
                profile,
                sample,
                windowMode,
                lookbackMinutes,
                cancellationToken);
            return Ok(payload);
        }

        [AllowAnonymous]
        [HttpPost("completed")]
        public async Task<IActionResult> Completed(
            [FromBody] CreditsCompletedRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || !await overlaySecurityService.ValidateKeyAsync(request.OverlayKey))
                return Unauthorized();

            await creditsService.TriggerCompletedAsync(
                request.ProfileName ?? "Default",
                Math.Max(0, request.TotalEntries),
                Math.Max(0, request.TotalSections),
                cancellationToken);

            return NoContent();
        }

        [HttpGet("export/profile/{id:guid}")]
        public async Task<IActionResult> ExportProfile(Guid id, CancellationToken cancellationToken = default)
        {
            var json = await creditsService.ExportProfileJsonAsync(id, cancellationToken);
            if (json == null) return NotFound();
            return Content(json, "application/json; charset=utf-8");
        }

        [HttpGet("export/theme/{id:guid}")]
        public async Task<IActionResult> ExportTheme(Guid id, CancellationToken cancellationToken = default)
        {
            var json = await creditsService.ExportThemeJsonAsync(id, cancellationToken);
            if (json == null) return NotFound();
            return Content(json, "application/json; charset=utf-8");
        }
    }

    public sealed record CreditsCompletedRequest(
        string? OverlayKey,
        string? ProfileName,
        int TotalEntries,
        int TotalSections);
}
