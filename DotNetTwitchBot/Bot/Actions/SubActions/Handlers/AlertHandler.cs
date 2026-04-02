using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using MediatR;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class AlertHandler(IMediator mediator, ILogger<AlertHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Alert;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not AlertType alertType)
            {
                logger.LogWarning("SubAction with type Alert is not of AlertType class");
                return;
            }

            try
            {
                alertType.Text = VariableReplacer.ReplaceVariables(alertType.Text, variables);
                alertType.Duration = int.Parse(VariableReplacer.ReplaceVariables(alertType.Duration.ToString(), variables));
                alertType.Volume = float.Parse(VariableReplacer.ReplaceVariables(alertType.Volume.ToString(), variables));
                alertType.CSS = VariableReplacer.ReplaceVariables(alertType.CSS, variables);
                alertType.File = VariableReplacer.ReplaceVariables(alertType.File, variables);
                await mediator.Publish(new QueueAlert(alertType.Generate()));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while replacing variables in Alert sub action");
            }
        }
    }
}
