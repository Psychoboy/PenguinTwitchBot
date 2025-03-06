using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class MultiCounterTagHandler(IServiceScopeFactory scopeFactory, IMediator mediator) : IRequestHandler<MultiCounterTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(MultiCounterTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var eventArgs = request.CommandEventArgs;
            var match = CustomCommand.CounterRegex().Match(args);
            var counterName = "";
            var counterAlert = "";

            if (match.Groups.Count > 0)
            {
                counterName = match.Groups[1].Value;
                counterAlert = match.Groups[2].Value;
            }
            else
            {
                counterName = args;
            }
            var amount = 0;
            //Fix counter here for alerts!
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var counter = await db.Counters.Find(x => x.CounterName.Equals(counterName)).FirstOrDefaultAsync();
                if (counter == null)
                {
                    counter = new Counter()
                    {
                        CounterName = counterName,
                        Amount = 0
                    };
                    await db.Counters.AddAsync(counter);
                }

                // TODO: Make this customizable
                if (eventArgs.Args.Count > 0 && (eventArgs.IsBroadcaster || eventArgs.IsMod))
                {
                    var modifier = eventArgs.Args[0];
                    int? minValue = null;
                    if (eventArgs.Args.Count > 1)
                    {
                        if (int.TryParse(eventArgs.Args[1], out var min))
                        {
                            minValue = min;
                        }
                    }

                    if (modifier.Equals("reset"))
                    {
                        counter.Amount = 0;
                    }
                    else if (modifier.Equals("+"))
                    {
                        counter.Amount++;
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
                    else if (modifier.Equals("set"))
                    {
                        if (eventArgs.Args.Count >= 2)
                        {
                            if (Int32.TryParse(eventArgs.Args[1], out var newAmount))
                            {
                                counter.Amount = newAmount;
                            }
                        }
                    }
                    await db.SaveChangesAsync();
                    await WriteCounterFile(counterName, counter.Amount);
                }
                amount = counter.Amount;
            }
            counterAlert = counterAlert.Replace("\\(totalcount\\)", amount.ToString());
            if (!string.IsNullOrWhiteSpace(counterAlert))
            {
                var alertImage = new AlertImage();
                await mediator.Publish(new QueueAlert(alertImage.Generate(counterAlert)));
            }

            return new CustomCommandResult(amount.ToString());
        }
        private async Task WriteCounterFile(string counterName, int amount)
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
