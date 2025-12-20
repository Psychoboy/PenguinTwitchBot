using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias
{
    public class Alias(
        IMediator mediator,
        IServiceBackbone serviceBackbone,
        ILogger<Alias> logger,
        ICommandHandler commandHandler) : BaseCommandService(serviceBackbone, commandHandler, "Alias", mediator), IAlias, IHostedService
    {
        public async Task<List<AliasModel>> GetAliasesAsync()
        {
            return (await mediator.Send(new GetAliases())).ToList();
        }

        public Task<AliasModel?> GetAliasAsync(int id)
        {
            return mediator.Send(new GetAliasById(id));
        }

        public Task CreateOrUpdateAliasAsync(AliasModel alias)
        {
            if (alias.Id == null)
            {
                return mediator.Send(new CreateAlias(alias));
            }
            else
            {
                return mediator.Send(new UpdateAlias(alias));
            }
        }

        public Task DeleteAliasAsync(AliasModel alias)
        {
            return mediator.Send(new DeleteAlias(alias));
        }

        public async Task<bool> RunCommand(CommandEventArgs e)
        {
            if (await IsAlias(e))
            {
                e.FromAlias = true;
                //e.SkipLock = true;
                await ServiceBackbone.RunCommand(e);
                return true;
            }
            return false;
        }

        public override Task<bool> OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.FromResult(true);
        }

        public async Task<bool> CommandExists(string alias)
        {
            return await mediator.Send(new GetAliasByName(alias)) != null;
        }

        private async Task<bool> IsAlias(CommandEventArgs e)
        {
            if (e.FromAlias) return false; //Prevents endless recursion

            var alias = await mediator.Send(new GetAliasByName(e.Command));
            if (alias != null)
            {
                e.Command = alias.CommandName;
                return true;
            }
            return false;
        }

        public override Task Register()
        {
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting Alias Service");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping Alias Service");
            return Task.CompletedTask;
        }
    }
}