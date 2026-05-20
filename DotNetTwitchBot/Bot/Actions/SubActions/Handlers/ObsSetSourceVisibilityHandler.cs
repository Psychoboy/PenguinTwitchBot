using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.ObsConnector;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetSourceVisibilityHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetSourceVisibilityHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetSourceVisibility;

        public ObsSetSourceVisibilityHandler(
            IOBSConnectionManager connectionManager,
            ILogger<ObsSetSourceVisibilityHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetSourceVisibilityType visibilityAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetSourceVisibility is not of ObsSetSourceVisibilityType class");
            }

            if (!visibilityAction.OBSConnectionId.HasValue)
            {
                throw new SubActionHandlerException(subAction, "ObsSetSourceVisibility SubAction missing connection ID");
            }

            var connection = _connectionManager.GetManagedConnection(visibilityAction.OBSConnectionId.Value);
            if (connection == null)
            {
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", visibilityAction.OBSConnectionId.Value);
            }

            if (!connection.IsConnected)
            {
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);
            }

            if (string.IsNullOrWhiteSpace(visibilityAction.SceneName))
            {
                throw new SubActionHandlerException(subAction, "Scene Name is required for ObsSetSourceVisibility");
            }

            if (string.IsNullOrWhiteSpace(visibilityAction.SourceName))
            {
                throw new SubActionHandlerException(subAction, "Source Name is required for ObsSetSourceVisibility");
            }

            var sceneName = VariableReplacer.ReplaceVariables(visibilityAction.SceneName, variables);
            var sourceName = VariableReplacer.ReplaceVariables(visibilityAction.SourceName, variables);

            try
            {
                connection.Execute(obs =>
                {
                    var sceneItemId = obs.GetSceneItemId(sceneName, sourceName, 0);
                    obs.SetSceneItemEnabled(sceneName, sceneItemId, visibilityAction.Visible);
                });

                var state = visibilityAction.Visible ? "visible" : "hidden";
                _logger.LogInformation(
                    "Set source '{Source}' in scene '{Scene}' to {State} in OBS connection '{Connection}'",
                    sourceName,
                    sceneName,
                    state,
                    connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting visibility of source '{Source}' in scene '{Scene}' in OBS connection '{Connection}'",
                    sourceName,
                    sceneName,
                    connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
