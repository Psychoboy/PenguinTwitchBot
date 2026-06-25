using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Queues;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetTextHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetTextHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetText;

        public ObsSetTextHandler(
            IOBSConnectionManager connectionManager,
            ILogger<ObsSetTextHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetTextType textAction)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetText is not of ObsSetTextType class");
            }

            if (!textAction.OBSConnectionId.HasValue)
            {
                throw new SubActionHandlerException(subAction, "ObsSetText SubAction missing connection ID");
            }

            var connection = _connectionManager.GetManagedConnection(textAction.OBSConnectionId.Value);
            if (connection == null)
            {
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", textAction.OBSConnectionId.Value);
            }

            if (!connection.IsConnected)
            {
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);
            }

            if (string.IsNullOrWhiteSpace(textAction.InputName))
            {
                throw new SubActionHandlerException(subAction, "Input Name is required for ObsSetText");
            }

            var inputName = VariableReplacer.ReplaceVariables(textAction.InputName, variables);
            var textContent = VariableReplacer.ReplaceVariables(textAction.TextContent, variables);

            try
            {
                connection.Execute(obs =>
                {
                    var settings = new JObject { { "text", textContent } };
                    obs.SetInputSettings(inputName, settings, overlay: true);
                });

                _logger.LogInformation(
                    "Set text source '{Input}' to '{Text}' in OBS connection '{Connection}'",
                    inputName,
                    textContent,
                    connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting text of input '{Input}' in OBS connection '{Connection}'",
                    inputName,
                    connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
