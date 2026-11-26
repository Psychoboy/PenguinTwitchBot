using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Alias;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Circuit;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Controllers;

[Route("api/twitch-extension")]
[ApiController]
[AllowAnonymous]
[EnableCors("TwitchExtensionCors")]
public partial class TwitchExtensionController : ControllerBase, IAsyncActionFilter
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
    private readonly IStarshipExpeditionService? _starshipExpeditionService;
    private readonly ITwitchExtensionTrackingService? _trackingService;

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
        ILogger<TwitchExtensionController> logger,
        IStarshipExpeditionService? starshipExpeditionService = null,
        ITwitchExtensionTrackingService? trackingService = null)
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
        _starshipExpeditionService = starshipExpeditionService;
        _trackingService = trackingService;
    }

    // Used for unit tests
    internal Task? LastTrackingTask { get; private set; }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        TwitchExtensionTokenValidationResult? validation = null;
        string? clientIp = null;

        try
        {
            validation = GetTokenValidationResult();
            clientIp = GetClientIpAddress();

            if (!validation.IsValid)
            {
                _trackingService?.RecordAuthFailure(validation.FailureReason);
            }
            else if (validation.Claims != null && validation.Claims.HasUserId && !string.IsNullOrWhiteSpace(clientIp))
            {
                if (!_ipLog.IsInteractionCached(validation.Claims.UserId!, clientIp))
                {
                    LastTrackingTask = Task.Run(() => TrackExtensionUserInteractionAsync(validation.Claims.UserId!, clientIp));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initiate tracking for Twitch extension request");
        }

        var executedContext = await next();

        try
        {
            if (_trackingService != null && validation?.Claims != null)
            {
                var actionMethodName = (context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?.ActionName;
                var (feature, endpoint, actionName) = ResolveActionTelemetry(actionMethodName);
                var statusCode = ResolveStatusCode(executedContext);
                var httpMethod = HttpContext.Request.Method;
                var claims = validation.Claims;

                Func<Task<string?>>? resolveUsernameAsync = claims.HasUserId
                    ? async () =>
                    {
                        var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(
                            claims.UserId!,
                            _viewerFeature,
                            _twitchService,
                            _logger);
                        return viewer?.Username;
                    }
                    : null;

                _trackingService.RecordRequest(
                    claims,
                    feature,
                    endpoint,
                    httpMethod,
                    statusCode,
                    clientIp,
                    actionName,
                    resolveUsernameAsync);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record Twitch extension request telemetry");
        }
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
        var starship = _featureCoordinator.IsEnabled(FeatureKeys.DropGames);

        return Ok(new ExtensionFeaturesResponse(
            Fishing: fishing,
            Giveaway: giveaway,
            Points: points,
            Leaderboards: true,
            Commands: true,
            Starship: starship));
    }

    #endregion

    #region Helper Methods

    private TwitchExtensionTokenValidationResult GetTokenValidationResult()
    {
        if (HttpContext.Items.TryGetValue("TwitchExtensionTokenValidation", out var cached) &&
            cached is TwitchExtensionTokenValidationResult result)
        {
            return result;
        }

        var authHeader = Request.Headers.Authorization.ToString();
        var validated = TwitchExtensionSecurity.ValidateTokenWithResult(authHeader, _configuration, _logger);
        HttpContext.Items["TwitchExtensionTokenValidation"] = validated;
        if (validated.Claims != null)
        {
            HttpContext.Items["TwitchExtensionClaims"] = validated.Claims;
        }

        return validated;
    }

    private TwitchExtensionClaims? GetClaims()
    {
        if (HttpContext.Items.TryGetValue("TwitchExtensionClaims", out var cached) && cached is TwitchExtensionClaims claims)
        {
            return claims;
        }

        return GetTokenValidationResult().Claims;
    }

    internal static (string Feature, string Endpoint, string? ActionName) ResolveActionTelemetry(string? actionName)
    {
        return actionName switch
        {
            nameof(GetFeatures) => ("features", "features", null),
            nameof(GetLeaderboards) => ("leaderboards", "leaderboards", null),
            nameof(GetLeaderboardsMeta) => ("leaderboards", "leaderboards_meta", null),
            nameof(GetGiveaway) => ("giveaway", "giveaway", null),
            nameof(GetGiveawayViewer) => ("giveaway", "giveaway_viewer", null),
            nameof(EnterGiveaway) => ("giveaway", "giveaway_enter", "giveaway_enter"),
            nameof(GetFishingTournaments) => ("fishing", "fishing_tournaments", null),
            nameof(GetRecentCatches) => ("fishing", "fishing_recent_catches", null),
            nameof(GetFishingStore) => ("fishing", "fishing_store", null),
            nameof(GetFishingViewer) => ("fishing", "fishing_viewer", null),
            nameof(BuyFishingItem) => ("fishing", "fishing_buy", "fishing_buy"),
            nameof(EquipFishingItem) => ("fishing", "fishing_equip", "fishing_equip"),
            nameof(UnequipFishingItem) => ("fishing", "fishing_unequip", "fishing_unequip"),
            nameof(RepairFishingItem) => ("fishing", "fishing_repair", "fishing_repair"),
            nameof(GetStarshipViewer) => ("starship", "starship_viewer", null),
            nameof(JoinStarshipCrew) => ("starship", "starship_join", "starship_join"),
            nameof(VoteStarship) => ("starship", "starship_vote", "starship_vote"),
            nameof(GetCommands) => ("commands", "commands", null),
            nameof(GetCommandCategories) => ("commands", "commands_categories", null),
            _ => ("other", string.IsNullOrWhiteSpace(actionName) ? "unknown" : actionName.ToLowerInvariant(), null)
        };
    }

    private int ResolveStatusCode(ActionExecutedContext executedContext)
    {
        if (executedContext.Exception != null && !executedContext.ExceptionHandled)
        {
            return StatusCodes.Status500InternalServerError;
        }

        if (executedContext.Result is Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult statusCodeResult &&
            statusCodeResult.StatusCode.HasValue)
        {
            return statusCodeResult.StatusCode.Value;
        }

        return HttpContext.Response.StatusCode > 0
            ? HttpContext.Response.StatusCode
            : StatusCodes.Status200OK;
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

    #endregion
}

#region DTO Records

public record ExtensionFeaturesResponse(bool Fishing, bool Giveaway, bool Points, bool Leaderboards, bool Commands, bool Starship = false);

public record ExtensionLeaderboardResponse(string Title, string ScoreLabel, List<ExtensionLeaderboardEntryResponse> Entries);
public record ExtensionLeaderboardEntryResponse(int Rank, string Username, double Score, string FormattedScore);
public record ExtensionPointTypeMeta(Guid Id, string Name);

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

public record ExtensionTournamentResponse(Guid Id, string Name, string ScoreCategory, List<ExtensionTournamentStandingResponse> Standings);
public record ExtensionTournamentStandingResponse(int Rank, string Username, double Score, int CatchCount);
public record ExtensionRecentCatchResponse(string Username, string FishName, double Weight, DateTime CaughtAt);

public record ExtensionFishingShopItemResponse(
    Guid Id,
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
    Guid Id,
    Guid ShopItemId,
    string Name,
    string Description,
    string? EquipmentSlot,
    bool IsEquipped,
    int RemainingUses,
    double? CurrentDurability,
    double? MaxDurability,
    bool IsBroken,
    string BoostSummary,
    bool CanRepair = false,
    int? RepairCost = null);

public record ExtensionBuyItemRequest(Guid ShopItemId, int Quantity);
public record ExtensionEquipItemRequest(Guid UserBoostId);
public record ExtensionRepairItemRequest(Guid UserBoostId);

public record ExtensionCommandResponse(
    string Command,
    string Category,
    string Description,
    int Cost,
    int UserCooldown,
    int GlobalCooldown,
    string? Rank = null);

public record ExtensionStarshipVoteRequest(string ChoiceId, bool ConfirmRisk = false);
public record ExtensionStarshipViewerResponse(
    bool HasJoined,
    bool HasVoted,
    string? VotedChoiceId,
    bool HasPendingRisk,
    string? PendingRiskChoiceId,
    long? MaximumRiskLoss,
    string? RiskPointTypeName);

#endregion
