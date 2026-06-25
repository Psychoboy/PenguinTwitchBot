using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Queues;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetImageSourceFileHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetImageSourceFileHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetImageSourceFile;

        public ObsSetImageSourceFileHandler(IOBSConnectionManager connectionManager, ILogger<ObsSetImageSourceFileHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetImageSourceFileType imageAction)
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetImageSourceFile is not of ObsSetImageSourceFileType class");

            if (!imageAction.OBSConnectionId.HasValue)
                throw new SubActionHandlerException(subAction, "ObsSetImageSourceFile SubAction missing connection ID");

            var connection = _connectionManager.GetManagedConnection(imageAction.OBSConnectionId.Value);
            if (connection == null)
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", imageAction.OBSConnectionId.Value);

            if (!connection.IsConnected)
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);

            if (string.IsNullOrWhiteSpace(imageAction.InputName))
                throw new SubActionHandlerException(subAction, "Input Name is required for ObsSetImageSourceFile");

            if (string.IsNullOrWhiteSpace(imageAction.FilePath))
                throw new SubActionHandlerException(subAction, "File Path is required for ObsSetImageSourceFile");

            var inputName = VariableReplacer.ReplaceVariables(imageAction.InputName, variables);
            var filePath = VariableReplacer.ReplaceVariables(imageAction.FilePath, variables);

            try
            {
                connection.Execute(obs =>
                {
                    var settings = new JObject { { "file", filePath } };
                    obs.SetInputSettings(inputName, settings, overlay: true);
                });

                _logger.LogInformation("Set image source '{Input}' file to '{File}' in OBS connection '{Connection}'",
                    inputName, filePath, connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting file of image source '{Input}' in OBS connection '{Connection}'",
                    inputName, connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
