using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class PlinkoDropHandler(IDropGamesService dropGamesService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.PlinkoDrop;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not PlinkoDropType plinkoSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type PlinkoDrop is not of PlinkoDropType class");
            }

            var resolvedUser = VariableReplacer.ReplaceVariables(plinkoSubAction.Username ?? "%User%", variables).Trim();
            if (string.IsNullOrWhiteSpace(resolvedUser) || resolvedUser.Equals("%User%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedUser = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "Viewer";
            }

            var resolvedEmote = VariableReplacer.ReplaceVariables(plinkoSubAction.EmoteOrImage ?? "", variables).Trim();
            if (resolvedEmote.Equals("%Args%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedEmote = "";
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(plinkoSubAction.ThemeOverrideId ?? "", variables).Trim();

            var eventArgs = CommandEventArgsConverter.FromDictionaryOrNull(variables) ?? new CommandEventArgs
            {
                Name = resolvedUser.ToLowerInvariant(),
                DisplayName = resolvedUser,
                Arg = resolvedEmote,
                FromOwnChannel = true
            };

            var userChanged = !string.Equals(eventArgs.DisplayName, resolvedUser, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(eventArgs.Name, resolvedUser, StringComparison.OrdinalIgnoreCase);

            eventArgs.DisplayName = resolvedUser;
            eventArgs.Name = resolvedUser.ToLowerInvariant();
            if (userChanged)
            {
                eventArgs.UserId = string.Empty;
            }

            context?.LogMessage(subActionIndex, $"Triggering Plinko Bounce for {resolvedUser}");
            await dropGamesService.TriggerPlinkoDropAsync(eventArgs, resolvedEmote, resolvedTheme, allowDirectUrlOrPath: true);
        }
    }
}

