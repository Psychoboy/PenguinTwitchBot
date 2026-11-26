using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Controllers;

public partial class TwitchExtensionController
{
    #region Starship Expedition

    [HttpGet("starship/viewer")]
    public async Task<IActionResult> GetStarshipViewer()
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission." });
        }

        if (_starshipExpeditionService == null)
        {
            return BadRequest(new { error = "service_unavailable", message = "Starship expedition service is not available." });
        }

        var snapshot = await _starshipExpeditionService.GetSnapshotAsync(HttpContext.RequestAborted);
        if (snapshot == null)
        {
            return Ok(new ExtensionStarshipViewerResponse(
                HasJoined: false,
                HasVoted: false,
                VotedChoiceId: null,
                HasPendingRisk: false,
                PendingRiskChoiceId: null,
                MaximumRiskLoss: null,
                RiskPointTypeName: null));
        }

        var userId = claims.UserId!;
        var hasJoined = snapshot.Crew.Any(m => string.Equals(m.UserId, userId, StringComparison.OrdinalIgnoreCase));
        var currentVote = snapshot.CurrentEvent?.Votes.FirstOrDefault(v => string.Equals(v.UserId, userId, StringComparison.OrdinalIgnoreCase));
        var pendingRisk = snapshot.CurrentEvent?.PendingRiskConfirmations.FirstOrDefault(r => string.Equals(r.UserId, userId, StringComparison.OrdinalIgnoreCase));

        return Ok(new ExtensionStarshipViewerResponse(
            HasJoined: hasJoined,
            HasVoted: currentVote != null,
            VotedChoiceId: currentVote?.ChoiceId,
            HasPendingRisk: pendingRisk != null,
            PendingRiskChoiceId: pendingRisk?.ChoiceId,
            MaximumRiskLoss: pendingRisk?.MaximumLoss,
            RiskPointTypeName: pendingRisk?.PointTypeName));
    }

    [HttpPost("starship/join")]
    public async Task<IActionResult> JoinStarshipCrew()
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to join the starship crew." });
        }

        if (_starshipExpeditionService == null)
        {
            return BadRequest(new { error = "service_unavailable", message = "Starship expedition service is not available." });
        }

        var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(claims.UserId!, _viewerFeature, _twitchService, _logger);
        var eventArgs = new CommandEventArgs
        {
            UserId = claims.UserId!,
            Name = viewer?.Username ?? claims.UserId!,
            DisplayName = viewer?.DisplayName ?? viewer?.Username ?? "Crew Member"
        };

        try
        {
            var snapshot = await _starshipExpeditionService.JoinCrewAsync(eventArgs, HttpContext.RequestAborted);
            return Ok(new { success = true, message = "Successfully boarded the starship!", crewCount = snapshot.Crew.Count });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining starship crew for user {UserId}", claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error boarding the starship." });
        }
    }

    [HttpPost("starship/vote")]
    public async Task<IActionResult> VoteStarship([FromBody] ExtensionStarshipVoteRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to vote in the expedition." });
        }

        if (_starshipExpeditionService == null)
        {
            return BadRequest(new { error = "service_unavailable", message = "Starship expedition service is not available." });
        }

        if (string.IsNullOrWhiteSpace(request.ChoiceId))
        {
            return BadRequest(new { success = false, message = "Choice ID must be provided." });
        }

        var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(claims.UserId!, _viewerFeature, _twitchService, _logger);
        var eventArgs = new CommandEventArgs
        {
            UserId = claims.UserId!,
            Name = viewer?.Username ?? claims.UserId!,
            DisplayName = viewer?.DisplayName ?? viewer?.Username ?? "Crew Member"
        };

        try
        {
            if (request.ConfirmRisk)
            {
                var currentSnapshot = await _starshipExpeditionService.GetSnapshotAsync(HttpContext.RequestAborted);
                var pending = currentSnapshot?.CurrentEvent?.PendingRiskConfirmations.FirstOrDefault(item =>
                    string.Equals(item.UserId, claims.UserId, StringComparison.OrdinalIgnoreCase));

                if (pending == null)
                {
                    return BadRequest(new { success = false, message = "No pending risk confirmation found. Please choose the action again." });
                }

                if (!string.Equals(pending.ChoiceId, request.ChoiceId, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "The pending risk confirmation does not match the requested choice." });
                }

                await _starshipExpeditionService.ConfirmRiskActionAsync(eventArgs, HttpContext.RequestAborted);
                return Ok(new { success = true, confirmed = true, message = "Risky action confirmed and committed!", choiceId = request.ChoiceId });
            }

            var snapshot = await _starshipExpeditionService.VoteAsync(eventArgs, request.ChoiceId, HttpContext.RequestAborted);

            var riskConfirmation = snapshot.CurrentEvent?.PendingRiskConfirmations.FirstOrDefault(item =>
                string.Equals(item.UserId, claims.UserId, StringComparison.OrdinalIgnoreCase));

            if (riskConfirmation != null)
            {
                return Ok(new
                {
                    success = true,
                    requiresConfirmation = true,
                    risk = new
                    {
                        choiceId = riskConfirmation.ChoiceId,
                        pointTypeName = riskConfirmation.PointTypeName,
                        exposedBalance = riskConfirmation.ExposedBalance,
                        maximumLoss = riskConfirmation.MaximumLoss,
                        successChancePercent = riskConfirmation.SuccessChancePercent,
                        rewardMultiplierPercent = riskConfirmation.RewardMultiplierPercent,
                        revealedRarity = riskConfirmation.RevealedRarity
                    },
                    message = $"Risk check: potential loss up to {riskConfirmation.MaximumLoss} {riskConfirmation.PointTypeName}. Please confirm your vote."
                });
            }

            return Ok(new { success = true, confirmed = false, message = "Vote recorded successfully!", choiceId = request.ChoiceId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voting in starship expedition for user {UserId}", claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error recording starship vote." });
        }
    }

    #endregion
}
