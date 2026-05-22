using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetSourceMuteStateHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetSourceMuteStateHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetSourceMuteState;

        public ObsSetSourceMuteStateHandler(
            IOBSConnectionManager connectionManager,
            ILogger<ObsSetSourceMuteStateHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetSourceMuteStateType muteAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetSourceMuteState is not of ObsSetSourceMuteStateType class");
            }

            if (!muteAction.OBSConnectionId.HasValue)
            {
                throw new SubActionHandlerException(subAction, "ObsSetSourceMuteState SubAction missing connection ID");
            }

            var connection = _connectionManager.GetManagedConnection(muteAction.OBSConnectionId.Value);
            if (connection == null)
            {
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", muteAction.OBSConnectionId.Value);
            }

            if (!connection.IsConnected)
            {
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);
            }

            if (string.IsNullOrWhiteSpace(muteAction.InputName))
            {
                throw new SubActionHandlerException(subAction, "Input Name is required for ObsSetSourceMuteState");
            }

            var inputName = VariableReplacer.ReplaceVariables(muteAction.InputName, variables);

            try
            {
                connection.Execute(obs =>
                {
                    obs.SetInputMute(inputName, muteAction.Muted);
                });

                var state = muteAction.Muted ? "muted" : "unmuted";
                _logger.LogInformation(
                    "Set input '{Input}' to {State} in OBS connection '{Connection}'",
                    inputName,
                    state,
                    connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting mute state of input '{Input}' in OBS connection '{Connection}'",
                    inputName,
                    connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
