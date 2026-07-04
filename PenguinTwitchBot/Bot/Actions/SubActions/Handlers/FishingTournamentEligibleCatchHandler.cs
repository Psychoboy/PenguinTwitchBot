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
            if (subAction is not FishingTournamentEligibleCatchType)
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
                .Where(tournament => tournament.EligibleFish.Count == 0 || tournament.EligibleFish.Any(fish => fish.FishTypeId == fishTypeId))
                .OrderBy(tournament => tournament.StartsAtUtc)
                .ThenBy(tournament => tournament.Name)
                .ToList();

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
        }
    }
}