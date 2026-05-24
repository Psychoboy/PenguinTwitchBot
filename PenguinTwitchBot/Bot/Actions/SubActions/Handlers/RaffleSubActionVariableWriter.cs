using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Commands.TicketGames;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    internal static class RaffleSubActionVariableWriter
    {
        public static void Write(ConcurrentDictionary<string, string> variables, RaffleOperationResult result)
        {
            variables["raffle_success"] = result.Success.ToString().ToLowerInvariant();
            variables["raffle_status"] = result.Status;
            variables["raffle_key"] = result.RaffleKey;
            variables["raffle_name"] = result.RaffleName;
            variables["raffle_join_command"] = result.JoinCommand;
            variables["raffle_point_game"] = result.PointGameName;
            variables["raffle_is_active"] = result.IsActive.ToString().ToLowerInvariant();
            variables["raffle_entry_count"] = result.EntryCount.ToString();
            variables["raffle_winner_count"] = result.WinnerCount.ToString();
            variables["raffle_total_award"] = result.TotalAward.ToString();
            variables["raffle_username"] = result.Username;
            variables["raffle_joined"] = result.Joined.ToString().ToLowerInvariant();
            variables["raffle_already_entered"] = result.AlreadyEntered.ToString().ToLowerInvariant();
            variables["raffle_each_award"] = result.EachAward.ToString();
            variables["raffle_awarded_total"] = result.AwardedTotal.ToString();
            variables["raffle_resolved_winner_count"] = result.ResolvedWinnerCount.ToString();
            variables["raffle_has_entries"] = (result.EntryCount > 0).ToString().ToLowerInvariant();
            variables["raffle_winners"] = string.Join(", ", result.Winners);

            for (var index = 0; index < result.Winners.Count; index++)
            {
                variables[$"raffle_winner_{index + 1}"] = result.Winners[index];
            }
        }
    }
}