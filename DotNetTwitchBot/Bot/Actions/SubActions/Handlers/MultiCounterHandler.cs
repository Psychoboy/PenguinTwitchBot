using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Actions.Utilities;
using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class MultiCounterHandler(IServiceScopeFactory scopeFactory) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.MultiCounter;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if(subAction is not MultiCounterType multiCounterSubAction)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type. Expected MultiCounterSubAction.");
            }

            var counterName = multiCounterSubAction.Name;

            int? minValue = multiCounterSubAction.Min;
            int? maxValue = multiCounterSubAction.Max;

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = await db.Counters.Find(x => x.CounterName.Equals(counterName)).FirstOrDefaultAsync();
            counter ??= new Counter()
            {
                CounterName = counterName,
                Amount = 0
            };

            var eventArgs = CommandEventArgsConverter.FromDictionary(variables);
            if(eventArgs.Args.Count > 0 && (eventArgs.IsBroadcaster || eventArgs.IsMod))
            {
                var modifier = eventArgs.Args[0];
                if (modifier.Equals("reset"))
                {
                    counter.Amount = 0;
                }
                else if (modifier.Equals("+"))
                {
                    if (maxValue.HasValue && counter.Amount + 1 > maxValue.Value)
                    {
                        counter.Amount = maxValue.Value;
                    }
                    else
                    {
                        counter.Amount++;
                    }
                }
                else if (modifier.Equals("-"))
                {
                    if (minValue.HasValue && counter.Amount - 1 < minValue.Value)
                    {
                        counter.Amount = minValue.Value;
                    }
                    else
                    {
                        counter.Amount--;
                    }
                }
                else if (modifier.Equals("set") &&
                    eventArgs.Args.Count >= 2 &&
                    int.TryParse(eventArgs.Args[1], out var newAmount))
                {
                    if (minValue.HasValue && newAmount < minValue.Value)
                    {
                        counter.Amount = minValue.Value;
                    }
                    else if (maxValue.HasValue && newAmount > maxValue.Value)
                    {
                        counter.Amount = maxValue.Value;
                    }
                    else
                    {
                        counter.Amount = newAmount;
                    }

                }
                db.Counters.Update(counter);
                await db.SaveChangesAsync();
                await WriteCounterFile(counterName, counter.Amount);
            }
            variables[$"counter_{counterName}"] = counter.Amount.ToString();
        }
        private static async Task WriteCounterFile(string counterName, int amount)
        {
            if (!Directory.Exists("Data/counters"))
            {
                Directory.CreateDirectory("Data/counters");
            }
            await File.WriteAllTextAsync($"Data/counters/{counterName}.txt", amount.ToString());
            await File.WriteAllTextAsync($"Data/counters/{counterName}-full.txt", counterName + ": " + amount.ToString());
        }
    }
}
