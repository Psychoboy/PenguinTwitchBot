using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingTournamentStartHandler(IFishingService fishingService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.FishingTournamentStart;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FishingTournamentStartType tournamentStart)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for FishingTournamentStartHandler: {SubActionType}", subAction.GetType().Name);
            }

            var tournament = await fishingService.StartFishingTournament(tournamentStart.TournamentId);
            FishingTournamentSubActionVariableWriter.Write(variables, tournament, tournament != null);

            if (tournament == null)
            {
                throw new SubActionHandlerException(subAction, "No fishing tournament found with ID: {TournamentId}", tournamentStart.TournamentId);
            }
        }
    }
}