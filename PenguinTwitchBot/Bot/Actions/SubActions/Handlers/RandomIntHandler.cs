using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RandomIntHandler() : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RandomInt;

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if(subAction is not RandomIntType randomInt)
            {
                throw new SubActionHandlerException(subAction, "RandomIntHandler received unsupported SubActionType: {SubActionType}", subAction.GetType().Name);
            }

            var value = RandomNumberGenerator.GetInt32(randomInt.Min, randomInt.Max + 1);
            variables["random_int"] = value.ToString();

            return Task.CompletedTask;
        }
    }
}
