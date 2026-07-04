using System.Collections.Concurrent;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    internal static class FishingTournamentSubActionVariableWriter
    {
        public static void Write(ConcurrentDictionary<string, string> variables, FishingTournament? tournament, bool success)
        {
            variables["fishing_tournament_success"] = success.ToString().ToLowerInvariant();
            variables["fishing_tournament_found"] = (tournament != null).ToString().ToLowerInvariant();

            if (tournament == null)
            {
                return;
            }

            variables["fishing_tournament_id"] = tournament.Id.ToString();
            variables["fishing_tournament_name"] = tournament.Name;
            variables["fishing_tournament_status"] = tournament.Status.ToString();
            variables["fishing_tournament_enabled"] = tournament.Enabled.ToString().ToLowerInvariant();
            variables["fishing_tournament_primary_score_category"] = tournament.PrimaryScoreCategory.ToString();
        }
    }
}