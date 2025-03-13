using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class MultiCounterAlertTagHandler(IServiceScopeFactory scopeFactory, IMediator mediator, ILogger<MultiCounterAlertTagHandler> logger) : IRequestHandler<MultiCounterAlertTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(MultiCounterAlertTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
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
                logger.LogWarning("Missing parameters for MultiCounterAlertTag");
                return new CustomCommandResult();
            }

            if(string.IsNullOrEmpty(counterName) || string.IsNullOrEmpty(counterAlert))
            {
                logger.LogWarning("Missing parameters for MultiCounterAlertTag");
                return new CustomCommandResult();
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var counter = await db.Counters.Find(x => x.CounterName.Equals(counterName)).FirstOrDefaultAsync(cancellationToken);
            if (counter == null)
            {
                counter = new Counter()
                {
                    CounterName = counterName,
                    Amount = 0
                };
                await db.Counters.AddAsync(counter);
                await db.SaveChangesAsync();
            }

            var amount = counter.Amount;
            counterAlert = counterAlert.Replace("\\(totalcount\\)", amount.ToString("N0"));
            var alertImage = new AlertImage();
            await mediator.Publish(new QueueAlert(alertImage.Generate(counterAlert)), cancellationToken);
            return new CustomCommandResult();
        }
    }
}
