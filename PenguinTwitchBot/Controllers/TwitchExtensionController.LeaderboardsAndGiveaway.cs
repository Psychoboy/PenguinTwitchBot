using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Bot;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Models;

namespace PenguinTwitchBot.Controllers;

public partial class TwitchExtensionController
{
    #region Leaderboards

    [HttpGet("leaderboards")]
    public async Task<IActionResult> GetLeaderboards(
        [FromQuery] string type = "points",
        [FromQuery] Guid? pointTypeId = null,
        [FromQuery] int top = 10)
    {
        var safeTop = Math.Clamp(top, 1, 50);

        if (string.Equals(type, "tournaments", StringComparison.OrdinalIgnoreCase))
        {
            return await GetFishingTournaments(safeTop);
        }

        if (string.Equals(type, "loudest", StringComparison.OrdinalIgnoreCase))
        {
            var loudest = await _leaderboards.GetLoudest(new PaginationFilter(0, safeTop));
            var response = loudest.Data.Select(x => new ExtensionLeaderboardEntryResponse(
                Rank: x.Rank,
                Username: x.Name,
                Score: x.Amount,
                FormattedScore: $"{x.Amount:N0} msgs")).ToList();

            return Ok(new ExtensionLeaderboardResponse("Loudest Chatters", "Messages", response));
        }

        if (string.Equals(type, "time", StringComparison.OrdinalIgnoreCase))
        {
            var timeResponse = await _leaderboards.GetTime(new PaginationFilter(0, safeTop));
            var response = timeResponse.Data.Select(x => new ExtensionLeaderboardEntryResponse(
                Rank: x.Rank,
                Username: x.Name,
                Score: x.Amount,
                FormattedScore: StaticTools.ConvertToCompoundDuration(Math.Max(0, x.Amount)))).ToList();

            return Ok(new ExtensionLeaderboardResponse("Watch Time", "Time Watched", response));
        }

        // Default: Points Leaderboard
        var pointTypes = await _pointsSystem.GetPointTypes();
        var targetPointType = pointTypeId.HasValue
            ? pointTypes.FirstOrDefault(p => p.GetId() == pointTypeId.Value)
            : pointTypes.FirstOrDefault();

        if (targetPointType == null)
        {
            return Ok(new ExtensionLeaderboardResponse("Points", "Points", []));
        }

        var pointsData = await _pointsSystem.GetLeaderPositions(new PaginationFilter(0, safeTop), targetPointType.GetId());
        var entries = pointsData.Data.Select(x => new ExtensionLeaderboardEntryResponse(
            Rank: x.Rank,
            Username: x.Name,
            Score: x.Amount,
            FormattedScore: $"{x.Amount:N0} {targetPointType.Name}")).ToList();

        return Ok(new ExtensionLeaderboardResponse(targetPointType.Name, targetPointType.Name, entries));
    }

    [HttpGet("leaderboards/meta")]
    public async Task<IActionResult> GetLeaderboardsMeta()
    {
        var pointTypes = await _pointsSystem.GetPointTypes();
        var response = pointTypes.Select(p => new ExtensionPointTypeMeta(p.GetId(), p.Name)).ToList();
        return Ok(response);
    }

    #endregion

    #region Giveaway

    [HttpGet("giveaway")]
    public async Task<IActionResult> GetGiveaway()
    {
        var isClosed = _giveawayFeature.IsClosed();
        var prize = await _giveawayFeature.GetPrize();
        var imageUrl = await _giveawayFeature.GetImageUrl();
        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            !imageUrl.StartsWith("//") &&
            (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            var relative = imageUrl.StartsWith('/') ? imageUrl : "/" + imageUrl;
            imageUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{relative}";
        }
        var pointsPerEntry = await _giveawayFeature.GetPointsPerEntry();
        var rules = await _giveawayFeature.GetRules();
        var additionalDetails = await _giveawayFeature.GetPrizeAdditionalDetails();
        var entrantsCount = await _giveawayFeature.GetEntrantsCount();
        var entriesCount = await _giveawayFeature.GetEntriesCount();

        return Ok(new ExtensionGiveawayResponse(
            IsClosed: isClosed,
            Prize: prize,
            ImageUrl: imageUrl,
            PointsPerEntry: pointsPerEntry,
            Rules: rules,
            AdditionalDetails: additionalDetails,
            EntrantsCount: entrantsCount,
            EntriesCount: entriesCount));
    }

    [HttpGet("giveaway/viewer")]
    public async Task<IActionResult> GetGiveawayViewer()
    {
        var claims = GetClaims();
        if (claims == null)
        {
            return Unauthorized(new { error = "auth_required", message = "Missing or invalid authorization token." });
        }

        var pointsPerEntry = await _giveawayFeature.GetPointsPerEntry();
        if (!claims.HasUserId)
        {
            return Ok(new ExtensionGiveawayViewerResponse(
                Username: string.Empty,
                UserTickets: 0,
                UserEntries: 0,
                PointsPerEntry: pointsPerEntry,
                MaxAffordableEntries: 0));
        }

        var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(claims.UserId!, _viewerFeature, _twitchService, _logger);
        if (viewer == null)
        {
            return Ok(new ExtensionGiveawayViewerResponse(
                Username: string.Empty,
                UserTickets: 0,
                UserEntries: 0,
                PointsPerEntry: pointsPerEntry,
                MaxAffordableEntries: 0));
        }
        var userTickets = (await _pointsSystem.GetUserPointsByUsernameAndGame(viewer.Username, "GiveawayFeature")).Points;
        var userEntries = await _giveawayFeature.GetEntriesCount(viewer.Username);
        var maxAffordable = pointsPerEntry > 0 ? userTickets / pointsPerEntry : 0;

        return Ok(new ExtensionGiveawayViewerResponse(
            Username: viewer.DisplayName,
            UserTickets: userTickets,
            UserEntries: userEntries,
            PointsPerEntry: pointsPerEntry,
            MaxAffordableEntries: maxAffordable));
    }

    [HttpPost("giveaway/enter")]
    public async Task<IActionResult> EnterGiveaway([FromBody] ExtensionGiveawayEnterRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to enter the giveaway." });
        }

        var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(claims.UserId!, _viewerFeature, _twitchService, _logger);
        if (viewer == null)
        {
            return NotFound(new { error = "viewer_not_found", message = "Viewer record not found." });
        }

        if (_giveawayFeature.IsClosed())
        {
            return BadRequest(new { success = false, message = "Giveaway is currently closed." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { success = false, message = "Amount must be greater than zero." });
        }

        try
        {
            var result = await _giveawayFeature.Enter(viewer.Username, request.Amount.ToString(), true);
            var newEntries = await _giveawayFeature.GetEntriesCount(viewer.Username);
            return Ok(new { success = true, message = result, entries = newEntries });
        }
        catch (SkipCooldownException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error entering giveaway for viewer {Username}", viewer.Username);
            return StatusCode(500, new { success = false, message = "Unexpected error entering giveaway." });
        }
    }

    #endregion
}
