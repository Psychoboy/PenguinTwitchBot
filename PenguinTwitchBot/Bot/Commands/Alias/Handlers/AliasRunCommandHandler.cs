using PenguinTwitchBot.Bot.Commands.Alias.Requests;
using PenguinTwitchBot.Bot.Features;

namespace PenguinTwitchBot.Bot.Commands.Alias.Handlers
{
    public class AliasRunCommandHandler(IAlias alias, ILogger<Alias> logger, IFeatureRuntimeCoordinator featureRuntimeCoordinator) : Application.Notifications.IRequestHandler<AliasRunCommand, bool>
    {
        public Task<bool> Handle(AliasRunCommand request, CancellationToken cancellationToken)
        {
            if(!featureRuntimeCoordinator.IsEnabled(FeatureKeys.Alias))
            {
                logger.LogInformation("Alias feature is disabled. Cannot run alias command.");
                return Task.FromResult(false);
            }
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
