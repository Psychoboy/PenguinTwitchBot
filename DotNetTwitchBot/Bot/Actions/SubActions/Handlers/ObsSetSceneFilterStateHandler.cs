using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.ObsConnector;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetSceneFilterStateHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetSceneFilterStateHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetSceneFilterState;

        public ObsSetSceneFilterStateHandler(
            IOBSConnectionManager connectionManager,
            ILogger<ObsSetSceneFilterStateHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not ObsSetSceneFilterStateType filterAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetSceneFilterState is not of ObsSetSceneFilterStateType class");
            }

            if (!filterAction.OBSConnectionId.HasValue)
            {
                throw new SubActionHandlerException(subAction, "ObsSetSceneFilterState SubAction missing connection ID");
            }

            var connection = _connectionManager.GetManagedConnection(filterAction.OBSConnectionId.Value);
            if (connection == null)
            {
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", filterAction.OBSConnectionId.Value);
            }

            if (!connection.IsConnected)
            {
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);
            }

            if (string.IsNullOrWhiteSpace(filterAction.SceneName))
            {
                throw new SubActionHandlerException(subAction, "Scene Name is required for ObsSetSceneFilterState");
            }

            if (string.IsNullOrWhiteSpace(filterAction.FilterName))
            {
                throw new SubActionHandlerException(subAction, "Filter Name is required for ObsSetSceneFilterState");
            }

            // Replace variables in scene and filter names
            var sceneName = VariableReplacer.ReplaceVariables(filterAction.SceneName, variables);
            var filterName = VariableReplacer.ReplaceVariables(filterAction.FilterName, variables);

            try
            {
                connection.Execute(obs =>
                {
                    obs.SetSourceFilterEnabled(sceneName, filterName, filterAction.FilterEnabled);
                });

                var state = filterAction.FilterEnabled ? "enabled" : "disabled";
                _logger.LogInformation(
                    "Set filter '{Filter}' on scene '{Scene}' to {State} in OBS connection '{Connection}'",
                    filterName,
                    sceneName,
                    state,
                    connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex, 
                    "Error setting filter '{Filter}' state on scene '{Scene}' in OBS connection '{Connection}'",
                    filterName,
                    sceneName,
                    connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
