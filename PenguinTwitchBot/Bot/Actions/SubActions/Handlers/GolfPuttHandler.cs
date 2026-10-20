using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Actions.Utilities;
using PenguinTwitchBot.Bot.Commands.Games.Golf;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class GolfPuttHandler(IGolfGameService golfGameService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.GolfPutt;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not GolfPuttType golfSubAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type GolfPutt is not of GolfPuttType class");
            }

            var resolvedUser = VariableReplacer.ReplaceVariables(golfSubAction.Username ?? "%User%", variables).Trim();
            if (string.IsNullOrWhiteSpace(resolvedUser) || resolvedUser.Equals("%User%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedUser = variables.TryGetValue("User", out var u) && !string.IsNullOrWhiteSpace(u) ? u : "Viewer";
            }

            var resolvedPowerStr = VariableReplacer.ReplaceVariables(golfSubAction.Power ?? "", variables).Trim();
            double? parsedPower = null;
            if (double.TryParse(resolvedPowerStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && double.IsFinite(p))
            {
                parsedPower = p;
            }

            var resolvedEmote = VariableReplacer.ReplaceVariables(golfSubAction.EmoteOrImage ?? "", variables).Trim();
            if (resolvedEmote.Equals("%Args%", StringComparison.OrdinalIgnoreCase))
            {
                resolvedEmote = "";
            }

            var resolvedTheme = VariableReplacer.ReplaceVariables(golfSubAction.ThemeOverrideId ?? "", variables).Trim();

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

            context?.LogMessage(subActionIndex, $"Triggering Golf Shot for {resolvedUser}");
            await golfGameService.TriggerGolfShotAsync(
                eventArgs,
                power: parsedPower,
                emoteOrImage: resolvedEmote,
                themeOverrideId: resolvedTheme,
                allowDirectUrlOrPath: true);
        }
    }
}

