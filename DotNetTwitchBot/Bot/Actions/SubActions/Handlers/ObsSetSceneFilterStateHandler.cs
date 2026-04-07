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
                _logger.LogWarning("SubAction with type ObsSetSceneFilterState is not of ObsSetSceneFilterStateType class");
                return Task.CompletedTask;
            }

            if (!filterAction.OBSConnectionId.HasValue)
            {
                _logger.LogWarning("ObsSetSceneFilterState SubAction missing connection ID");
                return Task.CompletedTask;
            }

            var connection = _connectionManager.GetManagedConnection(filterAction.OBSConnectionId.Value);
            if (connection == null)
            {
                _logger.LogWarning("OBS connection with ID {Id} not found", filterAction.OBSConnectionId.Value);
                return Task.CompletedTask;
            }

            if (!connection.IsConnected)
            {
                _logger.LogWarning("OBS connection '{Name}' is not connected", connection.Name);
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(filterAction.SceneName))
            {
                _logger.LogWarning("Scene Name is required for ObsSetSceneFilterState");
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(filterAction.FilterName))
            {
                _logger.LogWarning("Filter Name is required for ObsSetSceneFilterState");
                return Task.CompletedTask;
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
                _logger.LogError(ex, 
                    "Error setting filter '{Filter}' state on scene '{Scene}' in OBS connection '{Connection}'",
                    filterName,
                    sceneName,
                    connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
