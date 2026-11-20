using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingQuickTournamentHandler(
        IFishingService fishingService,
        IFishingFrenzyService frenzyService,
        IServiceBackbone serviceBackbone,
        IFishingMessageSettingsService? messageSettingsService = null) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.FishingQuickTournament;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FishingQuickTournamentType quickAction)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for FishingQuickTournamentHandler");
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(quickAction.Theme, variables);
            var resolvedScore = VariableReplacer.ReplaceVariables(quickAction.ScoreCategory, variables);
            Database.Bot.Models.Fishing.FishingTournamentScoreCategory? scoreOverride =
                !string.IsNullOrWhiteSpace(resolvedScore) &&
                !resolvedScore.Equals("Random", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<Database.Bot.Models.Fishing.FishingTournamentScoreCategory>(resolvedScore, true, out var parsedScore)
                    ? parsedScore
                    : null;

            var tournament = await fishingService.StartQuickTournamentAsync(new QuickTournamentRequest(
                Theme: string.IsNullOrWhiteSpace(resolvedTheme) ? "Random" : resolvedTheme,
                DurationMinutes: quickAction.DurationMinutes,
                ScoreCategoryOverride: scoreOverride));

            if (quickAction.PairWithFrenzy)
            {
                var targetCat = tournament.EligibleCategories.FirstOrDefault()?.Category;
                await frenzyService.StartFrenzyAsync(
                    $"{tournament.Name} Frenzy",
                    quickAction.DurationMinutes,
                    quickAction.FrenzyRarityMultiplier,
                    targetCat,
                    string.IsNullOrWhiteSpace(targetCat) ? 0.0 : 0.45,
                    announceInChat: false,
                    linkedTournamentId: tournament.Id);
            }

            variables["fishing_tournament_id"] = tournament.Id.ToString();
            variables["fishing_tournament_name"] = tournament.Name;
            variables["fishing_tournament_duration"] = tournament.RunDurationMinutes.ToString();

            if (quickAction.AnnounceInChat)
            {
                var msgs = messageSettingsService != null ? await messageSettingsService.GetAsync() : new FishingMessageSettings();
                var frenzyNote = quickAction.PairWithFrenzy ? $" 🔥 Frenzy ({quickAction.FrenzyRarityMultiplier:0.##}x) is ACTIVE!" : "";
                var msg = FishingMessageSettings.Format(msgs.QuickTournamentStart,
                    ("name", tournament.Name),
                    ("minutes", tournament.RunDurationMinutes.ToString()),
                    ("category", Helpers.FishingFormatHelper.FormatScoreCategory(tournament.PrimaryScoreCategory)),
                    ("frenzyNote", frenzyNote));
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    await serviceBackbone.SendChatMessage(msg);
                }
            }
        }
    }
}
