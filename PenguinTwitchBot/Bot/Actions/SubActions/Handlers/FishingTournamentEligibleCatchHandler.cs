using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingTournamentEligibleCatchHandler(IFishingService fishingService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.FishingTournamentEligibleCatch;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FishingTournamentEligibleCatchType eligibleCatch)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for FishingTournamentEligibleCatchHandler");
            }

            var fishTypeId = await ResolveFishTypeIdAsync(variables);
            if (fishTypeId <= 0)
            {
                SetNoMatchVariables(variables);
                return;
            }

            var tournaments = await fishingService.GetCurrentFishingTournaments();
            var matchingTournaments = tournaments
                .Where(tournament => tournament.Enabled && tournament.Status == Database.Bot.Models.Fishing.FishingTournamentStatus.Active)
                .Where(tournament => IsFishEligible(tournament, fishTypeId))
                .OrderBy(tournament => tournament.StartsAtUtc)
                .ThenBy(tournament => tournament.Name)
                .ToList();

            var userId = GetCurrentUserId(variables);
            var qualifyingPlacementOverride = Math.Max(0, eligibleCatch.QualifyingPlacementOverride);
            var qualifyingResults = new List<(int TournamentId, string TournamentName, int Rank, int MaxQualifyingPlacement)>();

            if (!string.IsNullOrWhiteSpace(userId) && matchingTournaments.Count > 0)
            {
                foreach (var tournament in matchingTournaments)
                {
                    var maxQualifyingPlacement = qualifyingPlacementOverride > 0
                        ? qualifyingPlacementOverride
                        : tournament.RewardRules.Where(rule => rule.Enabled).Select(rule => rule.Placement).DefaultIfEmpty(0).Max();

                    if (maxQualifyingPlacement <= 0)
                    {
                        continue;
                    }

                    var standings = await fishingService.GetFishingTournamentStandings(tournament.Id, maxQualifyingPlacement);
                    var standing = standings.FirstOrDefault(item => string.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase));
                    if (standing != null && standing.Rank <= maxQualifyingPlacement)
                    {
                        qualifyingResults.Add((tournament.Id, tournament.Name, standing.Rank, maxQualifyingPlacement));
                    }
                }
            }

            if (eligibleCatch.RequireQualifyingPosition)
            {
                var qualifyingTournamentIds = qualifyingResults.Select(result => result.TournamentId).ToHashSet();
                matchingTournaments = matchingTournaments
                    .Where(tournament => qualifyingTournamentIds.Contains(tournament.Id))
                    .ToList();
            }

            if (matchingTournaments.Count == 0)
            {
                SetNoMatchVariables(variables);
                return;
            }

            variables["fishing_tournament_eligible"] = "true";
            variables["fishing_tournament_match_count"] = matchingTournaments.Count.ToString();
            variables["fishing_tournament_ids"] = string.Join(",", matchingTournaments.Select(tournament => tournament.Id));
            variables["fishing_tournament_names"] = string.Join(", ", matchingTournaments.Select(tournament => tournament.Name));
            variables["fishing_tournament_primary_score_categories"] = string.Join(", ", matchingTournaments.Select(tournament => tournament.PrimaryScoreCategory.ToString()));
            variables["fishing_tournament_id"] = matchingTournaments[0].Id.ToString();
            variables["fishing_tournament_name"] = matchingTournaments[0].Name;
            variables["fishing_tournament_status"] = matchingTournaments[0].Status.ToString();
            variables["fishing_tournament_enabled"] = matchingTournaments[0].Enabled.ToString().ToLowerInvariant();
            variables["fishing_tournament_primary_score_category"] = matchingTournaments[0].PrimaryScoreCategory.ToString();
            variables["fishing_tournament_qualifying"] = qualifyingResults.Count > 0 ? "true" : "false";
            variables["fishing_tournament_qualifying_match_count"] = qualifyingResults.Count.ToString();
            variables["fishing_tournament_qualifying_ids"] = string.Join(",", qualifyingResults.Select(result => result.TournamentId));
            variables["fishing_tournament_qualifying_names"] = string.Join(", ", qualifyingResults.Select(result => result.TournamentName));
            variables["fishing_tournament_qualifying_rank"] = qualifyingResults.Count > 0 ? qualifyingResults[0].Rank.ToString() : string.Empty;
            variables["fishing_tournament_qualifying_max_place"] = qualifyingResults.Count > 0 ? qualifyingResults[0].MaxQualifyingPlacement.ToString() : string.Empty;
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

        private static bool IsFishEligible(Database.Bot.Models.Fishing.FishingTournament tournament, int fishTypeId)
        {
            var hasEligibleFish = tournament.EligibleFish.Count > 0;
            var hasEligibleCategories = tournament.EligibleCategories.Count > 0;

            // No fish and no categories selected means all fish are eligible (default behavior).
            if (!hasEligibleFish && !hasEligibleCategories)
            {
                return true;
            }

            if (hasEligibleFish && tournament.EligibleFish.Any(fish => fish.FishTypeId == fishTypeId))
            {
                return true;
            }

            if (hasEligibleCategories)
            {
                var fishCategories = tournament.EligibleFish
                    .Where(fish => fish.FishTypeId == fishTypeId)
                    .SelectMany(fish => fish.FishType?.Categories ?? [])
                    .Select(c => c.Category);

                if (fishCategories.Any(category => tournament.EligibleCategories.Any(selected => string.Equals(selected.Category, category, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<int> ResolveFishTypeIdAsync(ConcurrentDictionary<string, string> variables)
        {
            if (variables.TryGetValue("fish_type_id", out var fishTypeIdText) && int.TryParse(fishTypeIdText, out var fishTypeId) && fishTypeId > 0)
            {
                return fishTypeId;
            }

            if (!variables.TryGetValue("fish_type", out var fishTypeName) || string.IsNullOrWhiteSpace(fishTypeName))
            {
                return 0;
            }

            var fishTypes = await fishingService.GetAllFishTypes();
            return fishTypes.FirstOrDefault(fish => string.Equals(fish.Name, fishTypeName, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
        }

        private static void SetNoMatchVariables(ConcurrentDictionary<string, string> variables)
        {
            variables["fishing_tournament_eligible"] = "false";
            variables["fishing_tournament_match_count"] = "0";
            variables["fishing_tournament_ids"] = string.Empty;
            variables["fishing_tournament_names"] = string.Empty;
            variables["fishing_tournament_primary_score_categories"] = string.Empty;
            variables["fishing_tournament_id"] = string.Empty;
            variables["fishing_tournament_name"] = string.Empty;
            variables["fishing_tournament_status"] = string.Empty;
            variables["fishing_tournament_enabled"] = "false";
            variables["fishing_tournament_primary_score_category"] = string.Empty;
            variables["fishing_tournament_qualifying"] = "false";
            variables["fishing_tournament_qualifying_match_count"] = "0";
            variables["fishing_tournament_qualifying_ids"] = string.Empty;
            variables["fishing_tournament_qualifying_names"] = string.Empty;
            variables["fishing_tournament_qualifying_rank"] = string.Empty;
            variables["fishing_tournament_qualifying_max_place"] = string.Empty;
        }
    }
}