using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Bot.WebSocketEvents;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FireworksHandler(IWebSocketMessenger webSocketMessenger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Fireworks;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FireworksType fireworksType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for Fireworks handler");
            }

            var count = ResolveInteger(fireworksType.Count, variables, 1, 100, "Burst count");
            var delayMilliseconds = ResolveInteger(fireworksType.DelayMilliseconds, variables, 0, 60000, "Delay between bursts");
            var shell = VariableReplacer.ReplaceVariables(fireworksType.Shell, variables).Trim();
            var size = VariableReplacer.ReplaceVariables(fireworksType.Size, variables).Trim();
            if (!string.IsNullOrEmpty(size) &&
                (!double.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSize) ||
                 !double.IsFinite(parsedSize) || parsedSize < 0 || parsedSize > 5))
            {
                throw new ArgumentOutOfRangeException(nameof(fireworksType.Size), "Fireworks size must resolve to a number between 0 and 5, or be empty.");
            }

            await webSocketMessenger.AddToQueue(new WsEvent
            {
                Event = new WsEventType { Source = "SubAction", Type = "Fireworks" },
                Data = new Dictionary<string, object>
                {
                    ["count"] = count,
                    ["delayMs"] = delayMilliseconds,
                    ["shell"] = shell,
                    ["size"] = size
                }
            });
        }

        private static int ResolveInteger(string template, ConcurrentDictionary<string, string> variables, int min, int max, string label)
        {
            var resolved = VariableReplacer.ReplaceVariables(template, variables);
            if (!int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(label, $"{label} must resolve to a whole number between {min} and {max}.");
            }

            return value;
        }
    }
}