using DotNetTwitchBot.Bot.Commands.Alias.Requests;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class AliasRunCommandHandler(IAlias alias, ILogger<Alias> logger) : Application.Notifications.IRequestHandler<AliasRunCommand, bool>
    {
        public Task<bool> Handle(AliasRunCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.EventArgs == null) throw new ArgumentNullException();
                return alias.RunCommand(request.EventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error when running Alias.");
                return Task.FromResult(false);
            }
        }
    }
}
