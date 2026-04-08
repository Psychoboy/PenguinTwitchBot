using DotNetTwitchBot.Application.Alert.Notification;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class AlertHandler(Application.Notifications.IPenguinDispatcher dispatcher) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.Alert;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not AlertType alertType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type Alert is not of AlertType class");
            }

            try
            {
                alertType.Text = VariableReplacer.ReplaceVariables(alertType.Text, variables);
                alertType.Duration = int.Parse(VariableReplacer.ReplaceVariables(alertType.Duration.ToString(), variables));
                alertType.Volume = float.Parse(VariableReplacer.ReplaceVariables(alertType.Volume.ToString(), variables));
                alertType.CSS = VariableReplacer.ReplaceVariables(alertType.CSS, variables);
                alertType.File = VariableReplacer.ReplaceVariables(alertType.File, variables);
                await dispatcher.Publish(new QueueAlert(alertType.Generate()));
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, "Error while replacing variables in Alert sub action", ex);
            }
        }
    }
}
