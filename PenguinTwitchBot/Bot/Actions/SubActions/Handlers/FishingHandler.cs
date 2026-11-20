using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Helpers;
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
        private readonly IFishingAudioResolver? _audioResolver;
        private readonly IFishingOverlayAckService? _overlayAckService;
        public const string FishingTournamentCatchTriggerName = "FishingTournament.EligibleCatch";
        public const string FishCatchTriggerName = "Fishing.FishCaught";
        public const string FishingItemBrokenTriggerName = "Fishing.ItemBroken";
        public const string FishingAccidentTriggerName = "Fishing.Accident";

        public SubActionTypes SupportedType => SubActionTypes.Fishing;

        public FishingHandler(
            ILogger<FishingHandler> logger,
            IFishingService fishingService,
            IFishingGameplayService fishingGameplayService,
            IWebSocketMessenger webSocketMessenger,
            IServiceScopeFactory serviceScopeFactory,
            IFishingAudioResolver? audioResolver = null,
            IFishingOverlayAckService? overlayAckService = null)
        {
            _logger = logger;
            _fishingService = fishingService;
            _fishingGameplayService = fishingGameplayService;
            _webSocketMessenger = webSocketMessenger;
            _serviceScopeFactory = serviceScopeFactory;
            _audioResolver = audioResolver;
            _overlayAckService = overlayAckService;
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

            var attemptsText = VariableReplacer.ReplaceVariables(fishingAction.Text, variables)?.Trim();
            var attempts = Math.Clamp(fishingAction.Attempts, 1, 15);

            if (!string.IsNullOrWhiteSpace(attemptsText) &&
                int.TryParse(attemptsText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedAttempts))
            {
                attempts = Math.Clamp(parsedAttempts, 1, 15);
            }

            var totalGold = 0;
            var totalWeight = 0.0;
            var catchCount = 0;
            var accidentCount = 0;
            FishCatch? bestCatch = null;
            var summaryParts = new List<string>();
            var aggregatePoints = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            var shouldWaitForOverlay = fishingAction.WaitForReveal && _overlayAckService != null;
            var batchRevealId = shouldWaitForOverlay ? Guid.CreateVersion7() : Guid.Empty;
            if (shouldWaitForOverlay)
            {
                _overlayAckService!.Register(batchRevealId);
            }

            try
            {
                for (int i = 0; i < attempts; i++)
            {
                try
                {
                    var attemptResult = await _fishingGameplayService.PerformFishingAttempt(userId, username);

                    if (attemptResult.Outcome != FishingAttemptOutcome.CaughtFish)
                    {
                        accidentCount++;
                        var (failText, isRodSnap, isLineSnap, isReelJam, isTackleBoxLost, isNetBreak, logItem) = attemptResult.Outcome switch
                        {
                            FishingAttemptOutcome.RodSnapped => ("ROD SNAPPED", true, false, false, false, false, "rod"),
                            FishingAttemptOutcome.ReelJammed => ("REEL JAMMED", false, false, true, false, false, "reel"),
                            FishingAttemptOutcome.TackleBoxLost => ("TACKLE BOX LOST", false, false, false, true, false, "tackle box"),
                            FishingAttemptOutcome.NetBroken => ("NET BROKEN", false, false, false, false, true, "net"),
                            _ => ("LINE SNAPPED", false, true, false, false, false, "line")
                        };

                        var lostItemNames = attemptResult.SnapEvent?.LostItemsJson != null
                            ? DeserializeLostItemNames(attemptResult.SnapEvent.LostItemsJson)
                            : string.Empty;

                        var audio = _audioResolver?.ResolveForCatch(settings, FishRarity.Common, null);

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
                                lineSnapped = isLineSnap,
                                rodSnapped = isRodSnap,
                                reelJammed = isReelJam,
                                tackleBoxLost = isTackleBoxLost,
                                netBroken = isNetBreak,
                                isAccident = true,
                                accidentReason = failText,
                                lostItems = lostItemNames,
                                totalGoldLost = attemptResult.SnapEvent?.TotalGoldLost ?? 0,
                                batchIndex = i + 1,
                                batchTotal = attempts,
                                revealId = i == attempts - 1 && batchRevealId != Guid.Empty ? batchRevealId.ToString() : "",
                                enableCastAnimation = settings.EnableCastAnimation,
                                castAnimationMs = settings.CastAnimationMs,
                                soundEnabled = audio?.SoundEnabled ?? settings.SoundEnabled,
                                soundVolume = audio?.SoundVolume ?? settings.SoundVolume,
                                castSoundUrl = audio?.CastSoundUrl ?? FishingAudioResolver.DefaultCastSoundUrl,
                                biteSoundUrl = audio?.BiteSoundUrl ?? FishingAudioResolver.DefaultBiteSoundUrl,
                                catchSoundUrl = audio?.AccidentSoundUrl ?? FishingAudioResolver.DefaultAccidentSoundUrl,
                                accidentSoundUrl = audio?.AccidentSoundUrl ?? FishingAudioResolver.DefaultAccidentSoundUrl,
                                duration = settings.DisplayDurationMs
                            }
                        };

                        await _webSocketMessenger.AddToQueue(WsTopics.Fishing, JsonSerializer.Serialize(snappedMessage));

                        _logger.LogInformation("User {Username} had an accident ({Accident}) and caught nothing", username, logItem);
                        variables["fish_type"] = failText;
                        variables["fish_type_id"] = "0";
                        variables["fish_rarity"] = "Accident";
                        variables["fish_stars"] = "0";
                        variables["fish_weight"] = "0";
                        variables["fish_gold"] = "0";
                        variables["fish_points_awarded"] = string.Empty;
                        variables["fish_is_new_species"] = "false";
                        variables["fish_is_pb"] = "false";
                        variables["fish_is_server_record"] = "false";
                        summaryParts.Add(failText);

                        var accidentVariables = new ConcurrentDictionary<string, string>(variables);
                        accidentVariables["accident_type"] = attemptResult.Outcome.ToString();
                        accidentVariables["accident_name"] = failText;
                        accidentVariables["accident_reason"] = failText;
                        accidentVariables["accident_items_lost"] = lostItemNames;
                        accidentVariables["accident_gear"] = attemptResult.SnapEvent?.SnapType ?? string.Empty;
                        accidentVariables["accident_gold_lost"] = (attemptResult.SnapEvent?.TotalGoldLost ?? 0).ToString();
                        await TriggerFishingAccidentActionsAsync(accidentVariables, attemptResult.Outcome);

                        continue;
                    }

                    var fishCatch = attemptResult.FishCatch;
                    if (fishCatch == null)
                    {
                        throw new InvalidOperationException("Fishing attempt completed without catch data.");
                    }

                    catchCount++;
                    totalGold += fishCatch.GoldEarned;
                    totalWeight += fishCatch.Weight;
                    if (bestCatch == null || fishCatch.GoldEarned > bestCatch.GoldEarned)
                    {
                        bestCatch = fishCatch;
                    }

                    var rarity = fishCatch.FishType?.Rarity ?? FishRarity.Common;
                    var audioUrls = _audioResolver?.ResolveForCatch(settings, rarity, fishCatch.FishType?.ImageFileName);

                    var pointRewardsPayload = attemptResult.PointRewardsAwarded.Select(r => new
                    {
                        pointTypeName = r.PointTypeName,
                        amount = r.Amount,
                        formatted = $"+{FishingFormatHelper.FormatCompactNumber(r.Amount)} {r.PointTypeName}"
                    }).ToList();

                    foreach (var r in attemptResult.PointRewardsAwarded)
                    {
                        aggregatePoints[r.PointTypeName] = aggregatePoints.GetValueOrDefault(r.PointTypeName) + r.Amount;
                    }

                    var message = new
                    {
                        fishing = new
                        {
                            username = username,
                            fishName = fishCatch.FishType?.Name ?? "Unknown",
                            rarity = rarity.ToString(),
                            stars = fishCatch.Stars,
                            weight = fishCatch.Weight,
                            gold = fishCatch.GoldEarned,
                            imageFileName = fishCatch.FishType?.ImageFileName ?? "",
                            isNewSpecies = attemptResult.IsNewSpecies,
                            isPersonalBest = attemptResult.IsPersonalBest,
                            previousBestWeight = attemptResult.PreviousBestWeight,
                            isServerRecord = attemptResult.IsServerRecord,
                            isFrenzyBonus = attemptResult.IsFrenzyBonus,
                            frenzyName = attemptResult.ActiveFrenzyName ?? "",
                            triggeredBoosts = attemptResult.TriggeredBoosts.Select(b => new
                            {
                                itemName = b.ItemName,
                                reason = b.Reason,
                                remainingUses = b.RemainingUses,
                                maxUses = b.MaxUses
                            }).ToList(),
                            tournamentMatches = attemptResult.MatchedTournaments.Select(t => new
                            {
                                tournamentName = t.TournamentName,
                                rank = t.NewRank,
                                isLeader = t.IsLeader,
                                tookLead = t.TookLead
                            }).ToList(),
                            pointRewards = pointRewardsPayload,
                            brokenItems = attemptResult.BrokenItems.Select(b => b.ItemName).ToList(),
                            batchIndex = i + 1,
                            batchTotal = attempts,
                            revealId = i == attempts - 1 && batchRevealId != Guid.Empty ? batchRevealId.ToString() : "",
                            enableCastAnimation = settings.EnableCastAnimation,
                            castAnimationMs = settings.CastAnimationMs,
                            soundEnabled = audioUrls?.SoundEnabled ?? settings.SoundEnabled,
                            soundVolume = audioUrls?.SoundVolume ?? settings.SoundVolume,
                            castSoundUrl = audioUrls?.CastSoundUrl ?? FishingAudioResolver.DefaultCastSoundUrl,
                            biteSoundUrl = audioUrls?.BiteSoundUrl ?? FishingAudioResolver.DefaultBiteSoundUrl,
                            catchSoundUrl = audioUrls?.CatchSoundUrl ?? FishingAudioResolver.DefaultCatchCommonSoundUrl,
                            accidentSoundUrl = audioUrls?.AccidentSoundUrl ?? FishingAudioResolver.DefaultAccidentSoundUrl,
                            duration = settings.DisplayDurationMs
                        }
                    };

                    await _webSocketMessenger.AddToQueue(WsTopics.Fishing, JsonSerializer.Serialize(message));

                    _logger.LogInformation("User {Username} caught a {Stars} star {FishName} weighing {Weight}kg for {Gold} gold",
                        username, fishCatch.Stars, fishCatch.FishType?.Name, fishCatch.Weight, fishCatch.GoldEarned);
                    variables["fish_type"] = fishCatch.FishType?.Name ?? "Unknown";
                    variables["fish_type_id"] = fishCatch.FishTypeId.ToString();
                    variables["fish_rarity"] = rarity.ToString();
                    variables["fish_stars"] = fishCatch.Stars.ToString();
                    variables["fish_weight"] = fishCatch.Weight.ToString();
                    variables["fish_gold"] = fishCatch.GoldEarned.ToString();
                    variables["fish_is_new_species"] = attemptResult.IsNewSpecies ? "true" : "false";
                    variables["fish_is_pb"] = attemptResult.IsPersonalBest ? "true" : "false";
                    variables["fish_is_server_record"] = attemptResult.IsServerRecord ? "true" : "false";
                    variables["fish_points_awarded"] = string.Join(", ", pointRewardsPayload.Select(p => p.formatted));
                    summaryParts.Add($"{fishCatch.Stars}★ {fishCatch.FishType?.Name} ({fishCatch.GoldEarned}g)");

                    var fishCategoryNames = fishCatch.FishType?.Categories?.Select(c => c.Category).ToList() ?? [];
                    variables["fish_categories"] = string.Join(", ", fishCategoryNames);
                    await TriggerFishingTournamentCatchActionsAsync(variables, fishCatch.FishTypeId, fishCategoryNames);
                    await TriggerFishCatchActionsAsync(variables, fishCatch, fishCategoryNames);

                    if (attemptResult.BrokenItems.Count > 0)
                    {
                        await TriggerFishingItemBrokenActionsAsync(variables, attemptResult.BrokenItems);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing fishing attempt for user {Username}", username);
                    throw new SubActionHandlerException(subAction, $"Failed to perform fishing attempt: {ex.Message}");
                }
            }

            // Populate aggregate summary variables for multi-cast (and single-cast) recipes
            variables["fish_total_gold"] = totalGold.ToString();
            variables["fish_total_weight"] = Math.Round(totalWeight, 2).ToString("0.##");
            variables["fish_catch_count"] = catchCount.ToString();
            variables["fish_accident_count"] = accidentCount.ToString();
            variables["fish_best_name"] = bestCatch?.FishType?.Name ?? string.Empty;
            variables["fish_best_rarity"] = bestCatch?.FishType?.Rarity.ToString() ?? string.Empty;
            variables["fish_best_stars"] = (bestCatch?.Stars ?? 0).ToString();
            variables["fish_best_gold"] = (bestCatch?.GoldEarned ?? 0).ToString();
            variables["fish_summary"] = string.Join(", ", summaryParts);
            variables["fish_total_points_awarded"] = string.Join(", ", aggregatePoints.Select(kvp => $"+{FishingFormatHelper.FormatCompactNumber(kvp.Value)} {kvp.Key}"));

            // Two-way overlay sync: wait until the fishing overlay actually reveals this viewer's catch
            // (so when 5 viewers cast at once, chat announcements fire in sync with each viewer's splash!).
            if (fishingAction.WaitForReveal)
            {
                if (_overlayAckService != null && batchRevealId != Guid.Empty && await HasConnectedFishingOverlayAsync())
                {
                    var maxWait = TimeSpan.FromSeconds(120);
                    context?.BeginIntentionalDelay(maxWait);
                    var waitSw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        await _overlayAckService.WaitForRevealAsync(batchRevealId, maxWait);
                    }
                    finally
                    {
                        waitSw.Stop();
                        context?.AddCompletedIntentionalDelay(waitSw.Elapsed);
                        context?.EndIntentionalDelay();
                    }
                }
                else if (settings.EnableCastAnimation && _overlayAckService == null)
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Clamp(settings.CastAnimationMs, 300, 5000));
                    context?.BeginIntentionalDelay(delay);
                    var waitSw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        await Task.Delay(delay);
                    }
                    finally
                    {
                        waitSw.Stop();
                        context?.AddCompletedIntentionalDelay(waitSw.Elapsed);
                        context?.EndIntentionalDelay();
                    }
                }
            }
            }
            finally
            {
                if (shouldWaitForOverlay)
                {
                    _overlayAckService!.Unregister(batchRevealId);
                }
            }
        }

        private async Task<bool> HasConnectedFishingOverlayAsync()
        {
            try
            {
                var status = await _webSocketMessenger.GetStatusAsync();
                return status?.Connections?.Any(c =>
                    c.State == System.Net.WebSockets.WebSocketState.Open &&
                    c.Topics.Any(t => string.Equals(t, WsTopics.Fishing, StringComparison.OrdinalIgnoreCase))) == true;
            }
            catch
            {
                return false;
            }
        }

        private async Task TriggerFishingTournamentCatchActionsAsync(ConcurrentDictionary<string, string> variables, Guid fishTypeId, IEnumerable<string> fishCategoryNames)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.FishingTournamentCatch,
                    FishingTournamentCatchTriggerName);

                if (actions.Count == 0)
                {
                    return;
                }

                var tournaments = await _fishingService.GetCurrentFishingTournaments();
                var matchingTournaments = tournaments
                    .Where(tournament => tournament.Enabled && tournament.Status == FishingTournamentStatus.Active)
                    .Where(tournament => FishingCalculations.IsFishEligible(tournament, fishTypeId, fishCategoryNames))
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

        private async Task TriggerFishCatchActionsAsync(
            ConcurrentDictionary<string, string> variables,
            FishCatch fishCatch,
            IReadOnlyList<string> fishCategoryNames)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.FishCatch,
                    FishCatchTriggerName);

                if (actions.Count == 0)
                {
                    return;
                }

                List<FishingTournament>? activeTournaments = null;

                foreach (var action in actions)
                {
                    var triggers = action.Triggers
                        .Where(t => t.Type == TriggerTypes.FishCatch && t.Enabled && t.Name == FishCatchTriggerName)
                        .ToList();

                    if (triggers.Count == 0)
                    {
                        continue;
                    }

                    bool shouldExecute = false;

                    foreach (var trigger in triggers)
                    {
                        var config = DeserializeFishCatchConfiguration(trigger.Configuration);

                        if (config.FishTypeIds.Count > 0 && !config.FishTypeIds.Contains(fishCatch.FishTypeId))
                        {
                            continue;
                        }

                        if (config.Categories.Count > 0 &&
                            !fishCategoryNames.Any(c => config.Categories.Any(cc =>
                                string.Equals(cc, c, StringComparison.OrdinalIgnoreCase))))
                        {
                            continue;
                        }

                        var rarityName = fishCatch.FishType?.Rarity.ToString() ?? string.Empty;
                        if (config.Rarities.Count > 0 &&
                            !config.Rarities.Any(r => string.Equals(r, rarityName, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        if (config.MinStars > 0 && fishCatch.Stars < config.MinStars)
                        {
                            continue;
                        }

                        if (config.MinWeight > 0 && fishCatch.Weight < config.MinWeight)
                        {
                            continue;
                        }

                        if (config.MinGold > 0 && fishCatch.GoldEarned < config.MinGold)
                        {
                            continue;
                        }

                        if (config.RequireActiveTournament)
                        {
                            activeTournaments ??= await _fishingService.GetCurrentFishingTournaments();
                            var hasEligibleTournament = activeTournaments.Any(t =>
                                t.Enabled &&
                                t.Status == FishingTournamentStatus.Active &&
                                FishingCalculations.IsFishEligible(t, fishCatch.FishTypeId, fishCategoryNames));
                            if (!hasEligibleTournament)
                            {
                                continue;
                            }
                        }

                        shouldExecute = true;
                        break;
                    }

                    if (!shouldExecute)
                    {
                        continue;
                    }

                    await actionService.EnqueueAction(new ConcurrentDictionary<string, string>(variables), action);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering fish catch actions");
            }
        }

        private static FishCatchTriggerConfiguration DeserializeFishCatchConfiguration(string configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
            {
                return new FishCatchTriggerConfiguration();
            }

            try
            {
                return JsonSerializer.Deserialize<FishCatchTriggerConfiguration>(configuration)
                    ?? new FishCatchTriggerConfiguration();
            }
            catch
            {
                return new FishCatchTriggerConfiguration();
            }
        }

        private async Task TriggerFishingAccidentActionsAsync(
            ConcurrentDictionary<string, string> variables,
            FishingAttemptOutcome outcome)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.FishingAccident,
                    FishingAccidentTriggerName);

                if (actions.Count == 0)
                {
                    return;
                }

                foreach (var action in actions)
                {
                    var triggers = action.Triggers
                        .Where(t => t.Type == TriggerTypes.FishingAccident && t.Enabled && t.Name == FishingAccidentTriggerName)
                        .ToList();

                    if (triggers.Count == 0)
                    {
                        continue;
                    }

                    bool shouldExecute = false;

                    foreach (var trigger in triggers)
                    {
                        var config = DeserializeFishingAccidentConfiguration(trigger.Configuration);

                        if (config.AccidentTypes.Count > 0 && !config.AccidentTypes.Contains(outcome))
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

                    await actionService.EnqueueAction(new ConcurrentDictionary<string, string>(variables), action);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering fishing accident actions");
            }
        }

        private async Task TriggerFishingItemBrokenActionsAsync(
            ConcurrentDictionary<string, string> variables,
            IReadOnlyList<FishingBrokenItemInfo> brokenItems)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.FishingItemBroken,
                    FishingItemBrokenTriggerName);

                if (actions.Count == 0)
                {
                    return;
                }

                foreach (var action in actions)
                {
                    var triggers = action.Triggers
                        .Where(t => t.Type == TriggerTypes.FishingItemBroken && t.Enabled && t.Name == FishingItemBrokenTriggerName)
                        .ToList();

                    if (triggers.Count == 0)
                    {
                        continue;
                    }

                    foreach (var item in brokenItems)
                    {
                        var slotName = item.EquipmentSlot?.ToString() ?? string.Empty;
                        bool shouldExecute = false;

                        foreach (var trigger in triggers)
                        {
                            var config = DeserializeFishingItemBrokenConfiguration(trigger.Configuration);

                            if (config.EquipmentSlots.Count > 0 &&
                                !config.EquipmentSlots.Any(s => string.Equals(s, slotName, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }

                            if (config.ShopItemIds.Count > 0 && !config.ShopItemIds.Contains(item.ShopItemId))
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

                        var itemVariables = new ConcurrentDictionary<string, string>(variables)
                        {
                            ["broken_item_name"] = item.ItemName,
                            ["broken_item_slot"] = slotName,
                            ["broken_item_shop_id"] = item.ShopItemId.ToString(),
                            ["broken_item_boost_id"] = item.UserBoostId.ToString(),
                            ["broken_item_cost"] = item.ItemCost.ToString(),
                            ["broken_item_replaced"] = item.WasReplaced ? "true" : "false",
                            ["broken_items_count"] = brokenItems.Count.ToString()
                        };

                        await actionService.EnqueueAction(itemVariables, action);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering fishing item broken actions");
            }
        }

        private static FishingAccidentTriggerConfiguration DeserializeFishingAccidentConfiguration(string configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
            {
                return new FishingAccidentTriggerConfiguration();
            }

            try
            {
                var config = JsonSerializer.Deserialize<FishingAccidentTriggerConfiguration>(configuration)
                    ?? new FishingAccidentTriggerConfiguration();
                config.AccidentTypes ??= [];
                return config;
            }
            catch
            {
                return new FishingAccidentTriggerConfiguration();
            }
        }

        private static FishingItemBrokenTriggerConfiguration DeserializeFishingItemBrokenConfiguration(string configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
            {
                return new FishingItemBrokenTriggerConfiguration();
            }

            try
            {
                var config = JsonSerializer.Deserialize<FishingItemBrokenTriggerConfiguration>(configuration)
                    ?? new FishingItemBrokenTriggerConfiguration();
                config.EquipmentSlots ??= [];
                config.ShopItemIds ??= [];
                return config;
            }
            catch
            {
                return new FishingItemBrokenTriggerConfiguration();
            }
        }

        private static string DeserializeLostItemNames(string lostItemsJson)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<FishingSnapLostItem>>(lostItemsJson);
                if (items == null || items.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join(", ", items.Select(i => i.ItemName));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
