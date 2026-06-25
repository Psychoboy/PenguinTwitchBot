using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Queues;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetSourceAudioTrackStateHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetSourceAudioTrackStateHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetSourceAudioTrackState;

        public ObsSetSourceAudioTrackStateHandler(IOBSConnectionManager connectionManager, ILogger<ObsSetSourceAudioTrackStateHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetSourceAudioTrackStateType trackAction)
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetSourceAudioTrackState is not of ObsSetSourceAudioTrackStateType class");

            if (!trackAction.OBSConnectionId.HasValue)
                throw new SubActionHandlerException(subAction, "ObsSetSourceAudioTrackState SubAction missing connection ID");

            var connection = _connectionManager.GetManagedConnection(trackAction.OBSConnectionId.Value);
            if (connection == null)
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", trackAction.OBSConnectionId.Value);

            if (!connection.IsConnected)
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);

            if (string.IsNullOrWhiteSpace(trackAction.InputName))
                throw new SubActionHandlerException(subAction, "Input Name is required for ObsSetSourceAudioTrackState");

            if (trackAction.TrackNumber < 1 || trackAction.TrackNumber > 6)
                throw new SubActionHandlerException(subAction, "Track Number must be between 1 and 6 for ObsSetSourceAudioTrackState");

            var inputName = VariableReplacer.ReplaceVariables(trackAction.InputName, variables);

            try
            {
                connection.Execute(obs =>
                {
                    // Pass only the single track to modify; OBS applies as a patch
                    var tracks = new JObject { { trackAction.TrackNumber.ToString(), trackAction.TrackEnabled } };
                    obs.SetInputAudioTracks(inputName, tracks);
                });

                var state = trackAction.TrackEnabled ? "enabled" : "disabled";
                _logger.LogInformation("Set audio track {Track} on '{Input}' to {State} in OBS connection '{Connection}'",
                    trackAction.TrackNumber, inputName, state, connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting audio track {Track} on '{Input}' in OBS connection '{Connection}'",
                    trackAction.TrackNumber, inputName, connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
