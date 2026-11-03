using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Controllers;

[ApiController]
[Route("api/fireworks/queue")]
[Authorize(Roles = "Streamer, Editor")]
public sealed class FireworksQueueController(
    IUnitOfWork unitOfWork,
    IOverlaySecurityService overlaySecurityService,
    IFireworksQueueStatusStore statusStore) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("status")]
    public async Task<IActionResult> ReportStatus([FromBody] FireworksQueueStatusRequest request)
    {
        if (!await overlaySecurityService.ValidateKeyAsync(request.OverlayKey)) return Unauthorized();
        if (request.WidgetId == Guid.Empty ||
            request.PendingBursts < 0 ||
            request.MaxQueuedBursts is < 1 or > FireworksQueueDefaults.MaximumQueuedBursts ||
            request.MinLaunchGapMs is < 50 or > 3000 ||
            request.PendingBursts > request.MaxQueuedBursts ||
            request.RejectedBurstCount < 0 ||
            request.Jobs is { Count: > 100 })
        {
            return BadRequest();
        }

        if (!request.IsStandalone)
        {
            var widget = await unitOfWork.OverlayWidgets.GetByIdAsync(request.WidgetId);
            if (widget == null || !widget.WidgetType.Equals("fireworks", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }
        }

        var jobs = (request.Jobs ?? [])
            .Where(job => !string.IsNullOrWhiteSpace(job.JobId) && job.PendingBursts > 0)
            .ToList();
        if (jobs.Any(job => job.PendingBursts > request.MaxQueuedBursts) ||
            jobs.Sum(job => job.PendingBursts) > request.PendingBursts)
        {
            return BadRequest();
        }

        statusStore.Report(
            request.WidgetId,
            request.PendingBursts,
            request.MaxQueuedBursts,
            request.MinLaunchGapMs,
            request.RejectedBurstCount,
            jobs,
            request.IsStandalone);
        return NoContent();
    }
}

public sealed record FireworksQueueStatusRequest(
    Guid WidgetId,
    string OverlayKey,
    int PendingBursts,
    int MaxQueuedBursts,
    int RejectedBurstCount,
    List<FireworksQueueJobStatus>? Jobs,
    int MinLaunchGapMs = 240,
    bool IsStandalone = false);