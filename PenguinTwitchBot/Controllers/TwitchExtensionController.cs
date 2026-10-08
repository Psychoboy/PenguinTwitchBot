using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PenguinTwitchBot.Bot;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Alias;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Circuit;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Models;

namespace PenguinTwitchBot.Controllers;

[Route("api/twitch-extension")]
[ApiController]
[AllowAnonymous]
[EnableCors("TwitchExtensionCors")]
public class TwitchExtensionController : ControllerBase, IAsyncActionFilter
{
    private readonly IFishingService _fishingService;
    private readonly IFishingShopService _fishingShopService;
    private readonly IFishingInventoryService _fishingInventoryService;
    private readonly IGiveawayFeature _giveawayFeature;
    private readonly IPointsSystem _pointsSystem;
    private readonly Leaderboards _leaderboards;
    private readonly ICommandHandler _commandHandler;
    private readonly IActionCommandService _actionCommandService;
    private readonly IAlias _aliases;
    private readonly IFeatureRuntimeCoordinator _featureCoordinator;
    private readonly IViewerFeature _viewerFeature;
    private readonly ITwitchService _twitchService;
    private readonly IIpLog _ipLog;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwitchExtensionController> _logger;

    public TwitchExtensionController(
        IFishingService fishingService,
        IFishingShopService fishingShopService,
        IFishingInventoryService fishingInventoryService,
        IGiveawayFeature giveawayFeature,
        IPointsSystem pointsSystem,
        Leaderboards leaderboards,
        ICommandHandler commandHandler,
        IActionCommandService actionCommandService,
        IAlias aliases,
        IFeatureRuntimeCoordinator featureCoordinator,
        IViewerFeature viewerFeature,
        ITwitchService twitchService,
        IIpLog ipLog,
        IConfiguration configuration,
        ILogger<TwitchExtensionController> logger)
    {
        _fishingService = fishingService;
        _fishingShopService = fishingShopService;
        _fishingInventoryService = fishingInventoryService;
        _giveawayFeature = giveawayFeature;
        _pointsSystem = pointsSystem;
        _leaderboards = leaderboards;
        _commandHandler = commandHandler;
        _actionCommandService = actionCommandService;
        _aliases = aliases;
        _featureCoordinator = featureCoordinator;
        _viewerFeature = viewerFeature;
        _twitchService = twitchService;
        _ipLog = ipLog;
        _configuration = configuration;
        _logger = logger;
    }

    // Used for unit tests
    internal Task? LastTrackingTask { get; private set; }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try
        {
            var claims = GetClaims();
            var clientIp = GetClientIpAddress();
            if (claims != null && claims.HasUserId && !string.IsNullOrWhiteSpace(clientIp))
            {
                if (!_ipLog.IsInteractionCached(claims.UserId!, clientIp))
                {
                    LastTrackingTask = Task.Run(() => TrackExtensionUserInteractionAsync(claims.UserId!, clientIp));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initiate IP interaction tracking for Twitch extension request");
        }

        await next();
    }

    #region Feature Status & Metadata

    /// <summary>
    /// Returns live status of features enabled on the bot so panels can be conditionally shown or hidden.
    /// </summary>
    [HttpGet("features")]
    public IActionResult GetFeatures()
    {
        var fishing = _featureCoordinator.IsEnabled(FeatureKeys.Fishing);
        var giveaway = _featureCoordinator.IsEnabled(FeatureKeys.GiveawayFeature);
        var points = _featureCoordinator.IsEnabled(FeatureKeys.PointsSystem);

        return Ok(new ExtensionFeaturesResponse(
            Fishing: fishing,
            Giveaway: giveaway,
            Points: points,
            Leaderboards: true,
            Commands: true));
    }

    #endregion

    #region Leaderboards

    [HttpGet("leaderboards")]
    public async Task<IActionResult> GetLeaderboards(
        [FromQuery] string type = "points",
        [FromQuery] int? pointTypeId = null,
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

    #region Fishing

    [HttpGet("fishing-tournaments")]
    [HttpGet("fishing/tournaments")]
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
    [HttpGet("fishing/recent-catches")]
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

    [HttpGet("fishing/store")]
    public async Task<IActionResult> GetFishingStore()
    {
        var shopItems = await _fishingShopService.GetAllShopItems();
        var response = shopItems.Select(item => new ExtensionFishingShopItemResponse(
            Id: item.Id,
            Name: item.Name,
            Description: item.Description,
            Cost: item.Cost,
            EquipmentSlot: item.EquipmentSlot?.ToString(),
            BoostType: item.BoostType.ToString(),
            BoostAmount: item.BoostAmount,
            MaxUses: item.MaxUses,
            MaxDurability: item.MaxDurability,
            TargetFishName: item.TargetFishType?.Name)).ToList();

        return Ok(response);
    }

    [HttpGet("fishing/viewer")]
    public async Task<IActionResult> GetFishingViewer()
    {
        var claims = GetClaims();
        if (claims == null)
        {
            return Unauthorized(new { error = "auth_required", message = "Missing or invalid authorization token." });
        }

        if (!claims.HasUserId)
        {
            return Ok(new ExtensionFishingViewerResponse(
                Username: string.Empty,
                TotalGold: 0,
                Items: new List<ExtensionUserFishingBoostResponse>()));
        }

        var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(claims.UserId!, _viewerFeature, _twitchService, _logger);
        var gold = await _fishingService.GetUserGold(claims.UserId!);
        var boosts = await _fishingInventoryService.GetUserBoosts(claims.UserId!);

        var items = boosts.Select(b => new ExtensionUserFishingBoostResponse(
            Id: b.Id,
            ShopItemId: b.ShopItemId,
            Name: b.ShopItem?.Name ?? "Unknown Item",
            Description: b.ShopItem?.Description ?? string.Empty,
            EquipmentSlot: b.ShopItem?.EquipmentSlot?.ToString(),
            IsEquipped: b.IsEquipped,
            RemainingUses: b.RemainingUses,
            CurrentDurability: b.CurrentDurability,
            MaxDurability: b.ShopItem?.MaxDurability,
            IsBroken: b.IsBroken,
            BoostSummary: FormatBoostSummary(b.ShopItem))).ToList();

        return Ok(new ExtensionFishingViewerResponse(
            Username: viewer?.DisplayName ?? claims.UserId!,
            TotalGold: gold?.TotalGold ?? 0,
            Items: items));
    }

    [HttpPost("fishing/store/buy")]
    public async Task<IActionResult> BuyFishingItem([FromBody] ExtensionBuyItemRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to purchase items." });
        }

        try
        {
            var quantity = Math.Clamp(request.Quantity, 1, 100);
            await _fishingInventoryService.PurchaseBoost(claims.UserId!, request.ShopItemId, quantity);
            return Ok(new { success = true, message = "Item purchased successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purchasing shop item {ItemId} for user {UserId}", request.ShopItemId, claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error purchasing item." });
        }
    }

    [HttpPost("fishing/inventory/equip")]
    public async Task<IActionResult> EquipFishingItem([FromBody] ExtensionEquipItemRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to equip items." });
        }

        try
        {
            await _fishingInventoryService.EquipItem(claims.UserId!, request.UserBoostId);
            return Ok(new { success = true, message = "Item equipped successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error equipping item {BoostId} for user {UserId}", request.UserBoostId, claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error equipping item." });
        }
    }

    [HttpPost("fishing/inventory/unequip")]
    public async Task<IActionResult> UnequipFishingItem([FromBody] ExtensionEquipItemRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to unequip items." });
        }

        try
        {
            await _fishingInventoryService.UnequipItem(claims.UserId!, request.UserBoostId);
            return Ok(new { success = true, message = "Item unequipped successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unequipping item {BoostId} for user {UserId}", request.UserBoostId, claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error unequipping item." });
        }
    }

    #endregion

    #region Commands Lookup

    [HttpGet("commands")]
    public async Task<IActionResult> GetCommands([FromQuery] string? category = null, [FromQuery] string? search = null)
    {
        var allCommands = new List<ExtensionCommandResponse>();
        await AppendDefaultCommandsAsync(allCommands);
        await AppendPointCommandsAsync(allCommands);
        await AppendActionCommandsAsync(allCommands);
        await AppendExternalCommandsAsync(allCommands);

        var distinct = allCommands.DistinctBy(c => c.Command, StringComparer.OrdinalIgnoreCase).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        {
            distinct = distinct.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            distinct = distinct.Where(c =>
                c.Command.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(distinct.OrderBy(c => c.Command).ToList());
    }

    private async Task AppendDefaultCommandsAsync(List<ExtensionCommandResponse> allCommands)
    {
        var defaultCommands = await _commandHandler.GetDefaultCommandsFromDb();
        foreach (var cmd in defaultCommands.Where(x => !x.Disabled && !x.ExcludeFromUi && x.MinimumRank < Rank.Moderator))
        {
            allCommands.Add(new ExtensionCommandResponse(
                Command: "!" + cmd.CustomCommandName,
                Category: string.IsNullOrWhiteSpace(cmd.Category) ? (string.IsNullOrWhiteSpace(cmd.ModuleName) ? "General" : cmd.ModuleName) : cmd.Category,
                Description: cmd.Description ?? string.Empty,
                Cost: cmd.Cost,
                UserCooldown: cmd.UserCooldown,
                GlobalCooldown: cmd.GlobalCooldown));
        }
    }

    private async Task AppendPointCommandsAsync(List<ExtensionCommandResponse> allCommands)
    {
        var pointTypes = await _pointsSystem.GetPointTypes();
        foreach (var pt in pointTypes)
        {
            foreach (var cmd in pt.PointCommands.Where(x => !x.Disabled && !x.ExcludeFromUi && x.MinimumRank < Rank.Moderator))
            {
                var desc = cmd.CommandType == PointCommandType.Get
                    ? $"Checks your {pt.Name} balance."
                    : string.Empty;

                allCommands.Add(new ExtensionCommandResponse(
                    Command: "!" + cmd.CommandName,
                    Category: pt.Name,
                    Description: desc,
                    Cost: cmd.Cost,
                    UserCooldown: cmd.UserCooldown,
                    GlobalCooldown: cmd.GlobalCooldown));
            }
        }
    }

    private async Task AppendActionCommandsAsync(List<ExtensionCommandResponse> allCommands)
    {
        var actionCommands = await _actionCommandService.GetAllAsync();
        foreach (var cmd in actionCommands.Where(x => !x.Disabled && !x.ExcludeFromUi && x.MinimumRank < Rank.Moderator))
        {
            allCommands.Add(new ExtensionCommandResponse(
                Command: "!" + cmd.CommandName,
                Category: string.IsNullOrWhiteSpace(cmd.Category) ? "Actions" : cmd.Category,
                Description: cmd.Description ?? string.Empty,
                Cost: cmd.Cost,
                UserCooldown: cmd.UserCooldown,
                GlobalCooldown: cmd.GlobalCooldown));
        }
    }

    private async Task AppendExternalCommandsAsync(List<ExtensionCommandResponse> allCommands)
    {
        var externalCommands = await _commandHandler.GetExternalCommands();
        foreach (var cmd in externalCommands.Where(x => !x.Disabled && !x.ExcludeFromUi && x.MinimumRank < Rank.Moderator))
        {
            allCommands.Add(new ExtensionCommandResponse(
                Command: "!" + cmd.CommandName,
                Category: string.IsNullOrWhiteSpace(cmd.Category) ? "Custom" : cmd.Category,
                Description: cmd.Description ?? string.Empty,
                Cost: cmd.Cost,
                UserCooldown: cmd.UserCooldown,
                GlobalCooldown: cmd.GlobalCooldown));
        }
    }

    [HttpGet("commands/categories")]
    public async Task<IActionResult> GetCommandCategories()
    {
        var commandsResult = await GetCommands();
        if (commandsResult is OkObjectResult ok && ok.Value is List<ExtensionCommandResponse> list)
        {
            var categories = list
                .Select(c => c.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            return Ok(categories);
        }

        return Ok(new List<string>());
    }

    #endregion

    #region Helper Methods

    private TwitchExtensionClaims? GetClaims()
    {
        if (HttpContext.Items.TryGetValue("TwitchExtensionClaims", out var cached) && cached is TwitchExtensionClaims claims)
        {
            return claims;
        }

        var authHeader = Request.Headers.Authorization.ToString();
        var parsed = TwitchExtensionSecurity.ParseAndValidateToken(authHeader, _configuration, _logger);
        if (parsed != null)
        {
            HttpContext.Items["TwitchExtensionClaims"] = parsed;
        }
        return parsed;
    }

    private async Task TrackExtensionUserInteractionAsync(string userId, string clientIp)
    {
        try
        {
            if (_ipLog.IsInteractionCached(userId, clientIp))
            {
                return;
            }

            var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(
                userId,
                _viewerFeature,
                _twitchService,
                _logger);

            var username = viewer?.Username ?? userId;
            await _ipLog.LogInteractionAsync(username, userId, clientIp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track IP interaction for Twitch extension request");
        }
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }


    private static string FormatBoostSummary(FishingShopItem? item)
    {
        if (item == null) return string.Empty;
        var parts = new List<string>(3);
        if (item.BoostAmount != 0)
        {
            parts.Add($"{item.BoostType}: {item.BoostAmount:+0.##;-0.##}%");
        }
        if (item.BoostType2.HasValue && item.BoostAmount2.HasValue && item.BoostAmount2.Value != 0)
        {
            parts.Add($"{item.BoostType2.Value}: {item.BoostAmount2.Value:+0.##;-0.##}%");
        }
        if (item.BoostType3.HasValue && item.BoostAmount3.HasValue && item.BoostAmount3.Value != 0)
        {
            parts.Add($"{item.BoostType3.Value}: {item.BoostAmount3.Value:+0.##;-0.##}%");
        }
        return string.Join(", ", parts);
    }

    #endregion
}

#region DTO Records

public record ExtensionFeaturesResponse(bool Fishing, bool Giveaway, bool Points, bool Leaderboards, bool Commands);

public record ExtensionLeaderboardResponse(string Title, string ScoreLabel, List<ExtensionLeaderboardEntryResponse> Entries);
public record ExtensionLeaderboardEntryResponse(int Rank, string Username, double Score, string FormattedScore);
public record ExtensionPointTypeMeta(int Id, string Name);

public record ExtensionGiveawayResponse(
    bool IsClosed,
    string Prize,
    string? ImageUrl,
    int PointsPerEntry,
    string Rules,
    string AdditionalDetails,
    int EntrantsCount,
    int EntriesCount);

public record ExtensionGiveawayViewerResponse(
    string Username,
    long UserTickets,
    long UserEntries,
    int PointsPerEntry,
    long MaxAffordableEntries);

public record ExtensionGiveawayEnterRequest(int Amount);

public record ExtensionTournamentResponse(int Id, string Name, string ScoreCategory, List<ExtensionTournamentStandingResponse> Standings);
public record ExtensionTournamentStandingResponse(int Rank, string Username, double Score, int CatchCount);
public record ExtensionRecentCatchResponse(string Username, string FishName, double Weight, DateTime CaughtAt);

public record ExtensionFishingShopItemResponse(
    int Id,
    string Name,
    string Description,
    int Cost,
    string? EquipmentSlot,
    string BoostType,
    double BoostAmount,
    int? MaxUses,
    double? MaxDurability,
    string? TargetFishName);

public record ExtensionFishingViewerResponse(string Username, int TotalGold, List<ExtensionUserFishingBoostResponse> Items);
public record ExtensionUserFishingBoostResponse(
    int Id,
    int ShopItemId,
    string Name,
    string Description,
    string? EquipmentSlot,
    bool IsEquipped,
    int RemainingUses,
    double? CurrentDurability,
    double? MaxDurability,
    bool IsBroken,
    string BoostSummary);

public record ExtensionBuyItemRequest(int ShopItemId, int Quantity);
public record ExtensionEquipItemRequest(int UserBoostId);

public record ExtensionCommandResponse(
    string Command,
    string Category,
    string Description,
    int Cost,
    int UserCooldown,
    int GlobalCooldown);

#endregion