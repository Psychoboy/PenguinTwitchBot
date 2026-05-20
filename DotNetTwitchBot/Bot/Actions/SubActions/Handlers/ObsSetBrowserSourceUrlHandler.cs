using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.ObsConnector;
using DotNetTwitchBot.Bot.Queues;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetBrowserSourceUrlHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetBrowserSourceUrlHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetBrowserSourceUrl;

        public ObsSetBrowserSourceUrlHandler(IOBSConnectionManager connectionManager, ILogger<ObsSetBrowserSourceUrlHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetBrowserSourceUrlType urlAction)
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetBrowserSourceUrl is not of ObsSetBrowserSourceUrlType class");

            if (!urlAction.OBSConnectionId.HasValue)
                throw new SubActionHandlerException(subAction, "ObsSetBrowserSourceUrl SubAction missing connection ID");

            var connection = _connectionManager.GetManagedConnection(urlAction.OBSConnectionId.Value);
            if (connection == null)
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", urlAction.OBSConnectionId.Value);

            if (!connection.IsConnected)
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);

            if (string.IsNullOrWhiteSpace(urlAction.InputName))
                throw new SubActionHandlerException(subAction, "Input Name is required for ObsSetBrowserSourceUrl");

            if (string.IsNullOrWhiteSpace(urlAction.Url))
                throw new SubActionHandlerException(subAction, "URL is required for ObsSetBrowserSourceUrl");

            var inputName = VariableReplacer.ReplaceVariables(urlAction.InputName, variables);
            var url = VariableReplacer.ReplaceVariables(urlAction.Url, variables);

            try
            {
                connection.Execute(obs =>
                {
                    var settings = new JObject { { "url", url } };
                    obs.SetInputSettings(inputName, settings, overlay: true);
                });

                _logger.LogInformation("Set browser source '{Input}' URL to '{Url}' in OBS connection '{Connection}'",
                    inputName, url, connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting URL of browser source '{Input}' in OBS connection '{Connection}'",
                    inputName, connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
