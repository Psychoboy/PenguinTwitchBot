using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Controllers;

[Route("api/twitch-extension")]
[ApiController]
[AllowAnonymous]
public class TwitchExtensionController : ControllerBase
{
    private readonly IFishingService _fishingService;
    private readonly ILogger<TwitchExtensionController> _logger;

    public TwitchExtensionController(
        IFishingService fishingService,
        ILogger<TwitchExtensionController> logger)
    {
        _fishingService = fishingService;
        _logger = logger;
    }

    [HttpGet("fishing-tournaments")]
    public async Task<IActionResult> GetFishingTournaments([FromQuery] int top = 5)
    {
        var safeTop = Math.Clamp(top, 1, 20);

        var tournaments = await _fishingService.GetCurrentFishingTournaments();
        var activeTournaments = tournaments
            .Where(t => t.Status == FishingTournamentStatus.Active)
            .ToList();

        var response = new List<ExtensionTournamentResponse>(activeTournaments.Count);
        foreach (var tournament in activeTournaments)
        {
            var standings = await _fishingService.GetFishingTournamentStandings(tournament.Id, safeTop);
            response.Add(new ExtensionTournamentResponse(
                tournament.Id,
                tournament.Name,
                tournament.PrimaryScoreCategory.ToString(),
                standings.Select(s => new ExtensionTournamentStandingResponse(
                    s.Rank,
                    s.Username,
                    s.Score,
                    s.CatchCount)).ToList()));
        }

        return Ok(response);
    }

    [HttpGet("recent-catches")]
    public async Task<IActionResult> GetRecentCatches([FromQuery] int count = 20)
    {
        var safeCount = Math.Clamp(count, 1, 50);

        var catches = await _fishingService.GetRecentCatches(safeCount);

        var response = catches.Select(c => new ExtensionRecentCatchResponse(
            c.Username,
            c.FishType?.Name ?? "Unknown",
            c.Weight,
            c.CaughtAt)).ToList();

        return Ok(response);
    }
}

public record ExtensionTournamentResponse(int Id, string Name, string ScoreCategory, List<ExtensionTournamentStandingResponse> Standings);
public record ExtensionTournamentStandingResponse(int Rank, string Username, double Score, int CatchCount);
public record ExtensionRecentCatchResponse(string Username, string FishName, double Weight, DateTime CaughtAt);