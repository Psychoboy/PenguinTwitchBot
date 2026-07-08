using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingHandler : ISubActionHandler
    {
        private readonly ILogger<FishingHandler> _logger;
        private readonly IFishingService _fishingService;
        private readonly IFishingGameplayService _fishingGameplayService;
        private readonly IWebSocketMessenger _webSocketMessenger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private const string FishingTournamentCatchTriggerName = "FishingTournament.EligibleCatch";

        public SubActionTypes SupportedType => SubActionTypes.Fishing;

        public FishingHandler(
            ILogger<FishingHandler> logger,
            IFishingService fishingService,
            IFishingGameplayService fishingGameplayService,
            IWebSocketMessenger webSocketMessenger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _fishingService = fishingService;
            _fishingGameplayService = fishingGameplayService;
            _webSocketMessenger = webSocketMessenger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FishingType fishingAction)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for FishingHandler");
            }

            var settings = await _fishingService.GetSettings();
            if (settings?.Enabled != true)
            {
                _logger.LogWarning("Fishing is currently disabled");
                return;
            }

            if (!variables.TryGetValue("user", out var username) || string.IsNullOrEmpty(username))
            {
                throw new SubActionHandlerException(subAction, "User variable is required for fishing");
            }

            if (!variables.TryGetValue("userid", out var userId) || string.IsNullOrEmpty(userId))
            {
                throw new SubActionHandlerException(subAction, "UserId variable is required for fishing");
            }

            var attemptsText = VariableReplacer.ReplaceVariables(fishingAction.Text, variables);
            var attempts = fishingAction.Attempts;

            if (!string.IsNullOrWhiteSpace(attemptsText) && int.TryParse(attemptsText, out var parsedAttempts))
            {
                attempts = Math.Clamp(parsedAttempts, 1, 10);
            }

            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    var attemptResult = await _fishingGameplayService.PerformFishingAttempt(userId, username);

                    if (attemptResult.Outcome == FishingAttemptOutcome.LineSnapped ||
                        attemptResult.Outcome == FishingAttemptOutcome.RodSnapped)
                    {
                        var isRodSnap = attemptResult.Outcome == FishingAttemptOutcome.RodSnapped;
                        var failText = isRodSnap ? "ROD SNAPPED" : "LINE SNAPPED";

                        var snappedMessage = new
                        {
                            fishing = new
                            {
                                username = username,
                                fishName = failText,
                                rarity = "Accident",
                                stars = 0,
                                weight = 0.0,
                                gold = 0,
                                imageFileName = "",
                                lineSnapped = true,
                                rodSnapped = isRodSnap,
                                duration = settings.DisplayDurationMs
                            }
                        };

                        await _webSocketMessenger.AddToQueue(JsonSerializer.Serialize(snappedMessage));

                        if (attempts > 1 && i < attempts - 1)
                        {
                            await Task.Delay(settings.DisplayDurationMs + 500);
                        }

                        _logger.LogInformation("User {Username} had a snapped {SnapType} and caught nothing", username, isRodSnap ? "rod" : "line");
                        variables["fish_type"] = failText;
                        variables["fish_type_id"] = "0";
                        variables["fish_rarity"] = "Accident";
                        variables["fish_stars"] = "0";
                        variables["fish_weight"] = "0";
                        variables["fish_gold"] = "0";
                        continue;
                    }

                    var fishCatch = attemptResult.FishCatch;
                    if (fishCatch == null)
                    {
                        throw new InvalidOperationException("Fishing attempt completed without catch data.");
                    }

                    var message = new
                    {
                        fishing = new
                        {
                            username = username,
                            fishName = fishCatch.FishType?.Name ?? "Unknown",
                            rarity = fishCatch.FishType?.Rarity.ToString() ?? "Common",
                            stars = fishCatch.Stars,
                            weight = fishCatch.Weight,
                            gold = fishCatch.GoldEarned,
                            imageFileName = fishCatch.FishType?.ImageFileName ?? "",
                            duration = settings.DisplayDurationMs
                        }
                    };

                    await _webSocketMessenger.AddToQueue(JsonSerializer.Serialize(message));

                    if (attempts > 1 && i < attempts - 1)
                    {
                        await Task.Delay(settings.DisplayDurationMs + 500);
                    }

                    _logger.LogInformation("User {Username} caught a {Stars} star {FishName} weighing {Weight}kg for {Gold} gold",
                        username, fishCatch.Stars, fishCatch.FishType?.Name, fishCatch.Weight, fishCatch.GoldEarned);
                    variables["fish_type"] = fishCatch.FishType?.Name ?? "Unknown";
                    variables["fish_type_id"] = fishCatch.FishTypeId.ToString();
                    variables["fish_rarity"] = fishCatch.FishType?.Rarity.ToString() ?? "Common";
                    variables["fish_stars"] = fishCatch.Stars.ToString();
                    variables["fish_weight"] = fishCatch.Weight.ToString();
                    variables["fish_gold"] = fishCatch.GoldEarned.ToString();

                    await TriggerFishingTournamentCatchActionsAsync(variables, fishCatch.FishTypeId);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing fishing attempt for user {Username}", username);
                    throw new SubActionHandlerException(subAction, $"Failed to perform fishing attempt: {ex.Message}");
                }
            }
        }

        private async Task TriggerFishingTournamentCatchActionsAsync(ConcurrentDictionary<string, string> variables, int fishTypeId)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameAsync(
                    TriggerTypes.FishingTournamentCatch,
                    FishingTournamentCatchTriggerName);

                if (actions.Count == 0)
                {
                    return;
                }

                var tournaments = await _fishingService.GetCurrentFishingTournaments();
                var matchingTournaments = tournaments
                    .Where(tournament => tournament.Enabled && tournament.Status == FishingTournamentStatus.Active)
                    .Where(tournament => tournament.EligibleFish.Count == 0 || tournament.EligibleFish.Any(fish => fish.FishTypeId == fishTypeId))
                    .OrderBy(tournament => tournament.StartsAtUtc)
                    .ThenBy(tournament => tournament.Name)
                    .ToList();

                var userId = GetCurrentUserId(variables);

                foreach (var action in actions)
                {
                    var triggers = action.Triggers
                        .Where(trigger => trigger.Type == TriggerTypes.FishingTournamentCatch && trigger.Enabled && trigger.Name == FishingTournamentCatchTriggerName)
                        .ToList();

                    if (triggers.Count == 0)
                    {
                        continue;
                    }

                    bool shouldExecute = false;
                    List<(FishingTournament Tournament, int Rank, int MaxPlace)>? qualifyingMatches = null;

                    foreach (var trigger in triggers)
                    {
                        var config = DeserializeFishingTournamentCatchConfiguration(trigger.Configuration);

                        if (config.RequireEligibleTournamentFish && matchingTournaments.Count == 0)
                        {
                            continue;
                        }

                        qualifyingMatches = await GetQualifyingMatchesAsync(matchingTournaments, userId, config.QualifyingPlacementOverride);
                        if (config.RequireQualifyingPosition && !qualifyingMatches.Any(m => m.MaxPlace > 0 && m.Rank <= m.MaxPlace))
                        {
                            continue;
                        }

                        shouldExecute = true;
                        break;
                    }

                    if (!shouldExecute)
                    {
                        continue;
                    }

                    var actionVariables = new ConcurrentDictionary<string, string>(variables);
                    PopulateTournamentVariables(actionVariables, matchingTournaments, qualifyingMatches ?? []);
                    await actionService.EnqueueAction(actionVariables, action);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering fishing tournament catch actions");
            }
        }

        private static FishingTournamentCatchTriggerConfiguration DeserializeFishingTournamentCatchConfiguration(string configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
            {
                return new FishingTournamentCatchTriggerConfiguration();
            }

            try
            {
                return JsonSerializer.Deserialize<FishingTournamentCatchTriggerConfiguration>(configuration)
                    ?? new FishingTournamentCatchTriggerConfiguration();
            }
            catch
            {
                return new FishingTournamentCatchTriggerConfiguration();
            }
        }

        private async Task<List<(FishingTournament Tournament, int Rank, int MaxPlace)>> GetQualifyingMatchesAsync(
            List<FishingTournament> matchingTournaments,
            string? userId,
            int qualifyingPlacementOverride)
        {
            var qualifyingMatches = new List<(FishingTournament Tournament, int Rank, int MaxPlace)>();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return qualifyingMatches;
            }

            foreach (var tournament in matchingTournaments)
            {
                // Rank is always based on the tournament's PrimaryScoreCategory standings.
                var standings = await _fishingService.GetFishingTournamentStandings(tournament.Id, 100);
                var standing = standings.FirstOrDefault(item => string.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase));

                if (standing == null)
                {
                    continue;
                }

                // MaxPlace is used only to determine whether the user is in a reward-qualifying position.
                var maxPlace = qualifyingPlacementOverride > 0
                    ? qualifyingPlacementOverride
                    : tournament.RewardRules.Where(rule => rule.Enabled).Select(rule => rule.Placement).DefaultIfEmpty(0).Max();

                qualifyingMatches.Add((tournament, standing.Rank, maxPlace));
            }

            return qualifyingMatches;
        }

        private static void PopulateTournamentVariables(
            ConcurrentDictionary<string, string> variables,
            List<FishingTournament> matchingTournaments,
            List<(FishingTournament Tournament, int Rank, int MaxPlace)> qualifyingMatches)
        {
            variables["fishing_tournament_eligible"] = matchingTournaments.Count > 0 ? "true" : "false";
            variables["fishing_tournament_match_count"] = matchingTournaments.Count.ToString();
            variables["fishing_tournament_ids"] = string.Join(",", matchingTournaments.Select(tournament => tournament.Id));
            variables["fishing_tournament_names"] = string.Join(", ", matchingTournaments.Select(tournament => tournament.Name));
            variables["fishing_tournament_primary_score_categories"] = string.Join(", ", matchingTournaments.Select(tournament => tournament.PrimaryScoreCategory.ToString()));

            var firstTournament = matchingTournaments.FirstOrDefault();
            variables["fishing_tournament_id"] = firstTournament?.Id.ToString() ?? string.Empty;
            variables["fishing_tournament_name"] = firstTournament?.Name ?? string.Empty;
            variables["fishing_tournament_status"] = firstTournament?.Status.ToString() ?? string.Empty;
            variables["fishing_tournament_enabled"] = firstTournament != null ? firstTournament.Enabled.ToString().ToLowerInvariant() : "false";
            variables["fishing_tournament_primary_score_category"] = firstTournament?.PrimaryScoreCategory.ToString() ?? string.Empty;

            // "qualifying" = user has a rank in the tournament by PrimaryScoreCategory.
            variables["fishing_tournament_qualifying"] = qualifyingMatches.Count > 0 ? "true" : "false";
            variables["fishing_tournament_qualifying_rank"] = qualifyingMatches.Count > 0 ? qualifyingMatches[0].Rank.ToString() : string.Empty;
            variables["fishing_tournament_qualifying_match_count"] = qualifyingMatches.Count.ToString();
            variables["fishing_tournament_qualifying_ids"] = string.Join(",", qualifyingMatches.Select(item => item.Tournament.Id));
            variables["fishing_tournament_qualifying_names"] = string.Join(", ", qualifyingMatches.Select(item => item.Tournament.Name));

            // "reward_qualifying" = user would receive a reward if the tournament ended right now
            // (rank is within the enabled reward rule placement cutoff).
            var rewardQualifying = qualifyingMatches.Where(m => m.MaxPlace > 0 && m.Rank <= m.MaxPlace).ToList();
            variables["fishing_tournament_reward_qualifying"] = rewardQualifying.Count > 0 ? "true" : "false";
            variables["fishing_tournament_reward_qualifying_match_count"] = rewardQualifying.Count.ToString();
            variables["fishing_tournament_reward_qualifying_ids"] = string.Join(",", rewardQualifying.Select(item => item.Tournament.Id));
            variables["fishing_tournament_reward_qualifying_names"] = string.Join(", ", rewardQualifying.Select(item => item.Tournament.Name));
            variables["fishing_tournament_reward_qualifying_max_place"] = rewardQualifying.Count > 0 ? rewardQualifying[0].MaxPlace.ToString() : string.Empty;
        }

        private static string? GetCurrentUserId(ConcurrentDictionary<string, string> variables)
        {
            if (variables.TryGetValue("userid", out var userId) && !string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }

            if (variables.TryGetValue("user_id", out userId) && !string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }

            return null;
        }
    }
}
