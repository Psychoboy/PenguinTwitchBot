using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.ObsConnector;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetSceneHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetSceneHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetScene;

        public ObsSetSceneHandler(
            IOBSConnectionManager connectionManager,
            ILogger<ObsSetSceneHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not ObsSetSceneType setSceneType)
            {
                _logger.LogWarning("SubAction with type ObsSetScene is not of ObsSetSceneType class");
                return Task.CompletedTask;
            }

            if (!setSceneType.OBSConnectionId.HasValue)
            {
                _logger.LogWarning("ObsSetScene SubAction missing connection ID");
                return Task.CompletedTask;
            }

            var connection = _connectionManager.GetManagedConnection(setSceneType.OBSConnectionId.Value);
            if (connection == null)
            {
                _logger.LogWarning("OBS connection with ID {Id} not found", setSceneType.OBSConnectionId.Value);
                return Task.CompletedTask;
            }

            if (!connection.IsConnected)
            {
                _logger.LogWarning("OBS connection '{Name}' is not connected", connection.Name);
                return Task.CompletedTask;
            }

            // Replace variables in scene name
            var sceneName = VariableReplacer.ReplaceVariables(setSceneType.SceneName, variables);

            connection.Execute(obs => obs.SetCurrentProgramScene(sceneName));
            _logger.LogInformation("Set OBS scene to '{Scene}' on connection '{Connection}'", 
                sceneName, connection.Name);

            return Task.CompletedTask;
        }
    }
}
