using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class MultiCounterTagHandler(IServiceScopeFactory scopeFactory, ILogger<MultiCounterTagHandler> logger) : IRequestHandler<MultiCounterTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(MultiCounterTag request, CancellationToken cancellationToken)
        {
            
            var eventArgs = request.CommandEventArgs;
            var args = request.Args.Split(" ");
            var counterName = "";
            if(args.Length == 0)
            {
                logger.LogWarning("Missing parameters for MultiCounterTag");
                return new CustomCommandResult();
            }

            counterName = args[0];
            if(string.IsNullOrEmpty(counterName))
            {
                logger.LogWarning("Missing parameters for MultiCounterTag");
                return new CustomCommandResult();
            }

            int? minValue = null;
            int? maxValue = null;
            if (args.Length > 1)
            {
                if (Int32.TryParse(args[1], out var min))
                {
                    minValue = min;
                }
                if (args.Length > 2 && Int32.TryParse(args[2], out var max))
                {
                    maxValue = max;
                }
            }

            var amount = 0;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = await db.Counters.Find(x => x.CounterName.Equals(counterName)).FirstOrDefaultAsync(cancellationToken);
            counter ??= new Counter()
                {
                    CounterName = counterName,
                    Amount = 0
                };

            // TODO: Make this customizable
            if (eventArgs.Args.Count > 0 && (eventArgs.IsBroadcaster || eventArgs.IsMod))
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
                    Int32.TryParse(eventArgs.Args[1], out var newAmount))
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
            amount = counter.Amount;
            return new CustomCommandResult(amount.ToString("N0"));
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
