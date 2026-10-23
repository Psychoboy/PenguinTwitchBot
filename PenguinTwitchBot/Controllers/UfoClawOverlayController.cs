using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Bot.Commands.Games.UfoClaw;
using PenguinTwitchBot.Bot.Overlay;

namespace PenguinTwitchBot.Controllers;

[ApiController]
[Route("api/overlay/ufoclaw-state")]
public sealed class UfoClawOverlayController(IUfoClawService service, IOverlaySecurityService overlaySecurityService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get() => Ok(await service.GetOverlayAsync());

    [AllowAnonymous]
    [HttpPost("/api/overlay/ufoclaw-complete")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Complete([FromBody] UfoClawAnimationCompletionRequest request)
    {
        if (!await overlaySecurityService.ValidateKeyAsync(request.OverlayKey)) return Unauthorized();
        return await service.AcknowledgeAnimationAsync(request.GrabId) ? Accepted() : Conflict();
    }
}

public sealed record UfoClawAnimationCompletionRequest(Guid GrabId, string? OverlayKey);