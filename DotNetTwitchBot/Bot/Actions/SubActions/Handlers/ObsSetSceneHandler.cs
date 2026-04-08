using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.ObsConnector;
using System.Collections.Concurrent;

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

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if (subAction is not ObsSetSceneType setSceneType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetScene is not of ObsSetSceneType class");
            }

            if (!setSceneType.OBSConnectionId.HasValue)
            {
                throw new SubActionHandlerException(subAction, "ObsSetScene SubAction missing connection ID");
            }

            var connection = _connectionManager.GetManagedConnection(setSceneType.OBSConnectionId.Value) ?? throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", setSceneType.OBSConnectionId.Value);
            if (!connection.IsConnected)
            {
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);
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
