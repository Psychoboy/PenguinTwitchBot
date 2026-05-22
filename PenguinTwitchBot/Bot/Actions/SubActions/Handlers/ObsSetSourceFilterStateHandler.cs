using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetSourceFilterStateHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetSourceFilterStateHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetSourceFilterState;

        public ObsSetSourceFilterStateHandler(IOBSConnectionManager connectionManager, ILogger<ObsSetSourceFilterStateHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetSourceFilterStateType filterAction)
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetSourceFilterState is not of ObsSetSourceFilterStateType class");

            if (!filterAction.OBSConnectionId.HasValue)
                throw new SubActionHandlerException(subAction, "ObsSetSourceFilterState SubAction missing connection ID");

            var connection = _connectionManager.GetManagedConnection(filterAction.OBSConnectionId.Value);
            if (connection == null)
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", filterAction.OBSConnectionId.Value);

            if (!connection.IsConnected)
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);

            if (string.IsNullOrWhiteSpace(filterAction.SourceName))
                throw new SubActionHandlerException(subAction, "Source Name is required for ObsSetSourceFilterState");

            if (string.IsNullOrWhiteSpace(filterAction.FilterName))
                throw new SubActionHandlerException(subAction, "Filter Name is required for ObsSetSourceFilterState");

            var sourceName = VariableReplacer.ReplaceVariables(filterAction.SourceName, variables);
            var filterName = VariableReplacer.ReplaceVariables(filterAction.FilterName, variables);

            try
            {
                connection.Execute(obs =>
                {
                    obs.SetSourceFilterEnabled(sourceName, filterName, filterAction.FilterEnabled);
                });

                var state = filterAction.FilterEnabled ? "enabled" : "disabled";
                _logger.LogInformation("Set filter '{Filter}' on source '{Source}' to {State} in OBS connection '{Connection}'",
                    filterName, sourceName, state, connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting filter '{Filter}' state on source '{Source}' in OBS connection '{Connection}'",
                    filterName, sourceName, connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
