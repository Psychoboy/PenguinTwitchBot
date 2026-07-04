using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingTournamentEndHandler(IFishingService fishingService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.FishingTournamentEnd;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FishingTournamentEndType tournamentEnd)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for FishingTournamentEndHandler: {SubActionType}", subAction.GetType().Name);
            }

            var tournament = await fishingService.EndFishingTournament(tournamentEnd.TournamentId);
            FishingTournamentSubActionVariableWriter.Write(variables, tournament, tournament != null);

            if (tournament == null)
            {
                throw new SubActionHandlerException(subAction, "No fishing tournament found with ID: {TournamentId}", tournamentEnd.TournamentId);
            }
        }
    }
}