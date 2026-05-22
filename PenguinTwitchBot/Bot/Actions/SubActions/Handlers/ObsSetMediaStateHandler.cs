using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetMediaStateHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetMediaStateHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetMediaState;

        public ObsSetMediaStateHandler(IOBSConnectionManager connectionManager, ILogger<ObsSetMediaStateHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetMediaStateType mediaStateAction)
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetMediaState is not of ObsSetMediaStateType class");

            if (!mediaStateAction.OBSConnectionId.HasValue)
                throw new SubActionHandlerException(subAction, "ObsSetMediaState SubAction missing connection ID");

            var connection = _connectionManager.GetManagedConnection(mediaStateAction.OBSConnectionId.Value);
            if (connection == null)
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", mediaStateAction.OBSConnectionId.Value);

            if (!connection.IsConnected)
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);

            if (string.IsNullOrWhiteSpace(mediaStateAction.InputName))
                throw new SubActionHandlerException(subAction, "Input Name is required for ObsSetMediaState");

            if (string.IsNullOrWhiteSpace(mediaStateAction.MediaAction))
                throw new SubActionHandlerException(subAction, "Media Action is required for ObsSetMediaState");

            var inputName = VariableReplacer.ReplaceVariables(mediaStateAction.InputName, variables);
            var mediaAction = mediaStateAction.MediaAction;

            try
            {
                connection.Execute(obs =>
                {
                    obs.TriggerMediaInputAction(inputName, mediaAction);
                });

                _logger.LogInformation("Triggered media action '{Action}' on '{Input}' in OBS connection '{Connection}'",
                    mediaAction, inputName, connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error triggering media action '{Action}' on '{Input}' in OBS connection '{Connection}'",
                    mediaAction, inputName, connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
