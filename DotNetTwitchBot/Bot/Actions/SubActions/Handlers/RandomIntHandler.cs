using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using System.Security.Cryptography;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class RandomIntHandler(ILogger<RandomIntHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.RandomInt;

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if(subAction is not RandomIntType randomInt)
            {
                logger.LogWarning("RandomIntHandler received unsupported SubActionType: {SubActionType}", subAction.GetType().Name);
                return Task.CompletedTask;
            }

            var value = RandomNumberGenerator.GetInt32(randomInt.Min, randomInt.Max + 1);
            variables["random_int"] = value.ToString();
            
            return Task.CompletedTask;
        }
    }
}
