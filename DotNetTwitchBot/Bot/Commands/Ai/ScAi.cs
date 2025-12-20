using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Application.ChatMessage.Notifications;
using DotNetTwitchBot.Bot.Ai;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Ai
{
    public class ScAi(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IServiceScopeFactory scopeFactory,
        ILogger<ScAi> logger,
        IMediator mediator
        ) : BaseCommandService(serviceBackbone, commandHandler, "ScAi", mediator), IHostedService
    {
        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            if (!command.CommandProperties.CommandName.Equals("scai")) return;

            await Respond(e);
        }

        private async Task Respond(CommandEventArgs e)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var scAi = scope.ServiceProvider.GetService<IStarCitizenAI>();
            if (scAi == null)
            {
                logger.LogError("StarCitizenAI service is not available.");
                return;
            }

            if(string.IsNullOrEmpty(e.Arg))
            {
                await ResponseWithMessage(e, "Please provide a question or prompt for SCAI.");
                return;
            }

            try
            {
                var response = await scAi.GetResponseFromPrompt(e.Arg);
                if(string.IsNullOrWhiteSpace(response))
                {
                    logger.LogWarning("Received empty response from StarCitizenAI.");
                    await ResponseWithMessage(e, "Sorry, I couldn't get a response right now.");
                    //await mediator.Publish(new ReplyToMessage(e.MessageId, ));
                    return;
                }
                //await mediator.Publish(new ReplyToMessage(e.MessageId, response));
                await ResponseWithMessage(e, response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting response from StarCitizenAI.");
                //await mediator.Publish(new ReplyToMessage(e.MessageId, "Sorry, there was an error processing your request."));
                await ResponseWithMessage(e, "Sorry, there was an error processing your request.");
                return;
            }
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("scai", this, ModuleName, Rank.Subscriber, true, true, 60, 60, "Ask the AI questions related to Star Citizen");
            logger.LogInformation("Registered ScAi command");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {module}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {module}", ModuleName);
            return Task.CompletedTask;
        }

    }
}
