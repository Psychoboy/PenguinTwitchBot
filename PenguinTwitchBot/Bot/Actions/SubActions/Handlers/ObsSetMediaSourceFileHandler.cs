using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Bot.Queues;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetMediaSourceFileHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetMediaSourceFileHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetMediaSourceFile;

        public ObsSetMediaSourceFileHandler(IOBSConnectionManager connectionManager, ILogger<ObsSetMediaSourceFileHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetMediaSourceFileType mediaFileAction)
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetMediaSourceFile is not of ObsSetMediaSourceFileType class");

            if (!mediaFileAction.OBSConnectionId.HasValue)
                throw new SubActionHandlerException(subAction, "ObsSetMediaSourceFile SubAction missing connection ID");

            var connection = _connectionManager.GetManagedConnection(mediaFileAction.OBSConnectionId.Value);
            if (connection == null)
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", mediaFileAction.OBSConnectionId.Value);

            if (!connection.IsConnected)
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);

            if (string.IsNullOrWhiteSpace(mediaFileAction.InputName))
                throw new SubActionHandlerException(subAction, "Input Name is required for ObsSetMediaSourceFile");

            if (string.IsNullOrWhiteSpace(mediaFileAction.FilePath))
                throw new SubActionHandlerException(subAction, "File Path is required for ObsSetMediaSourceFile");

            var inputName = VariableReplacer.ReplaceVariables(mediaFileAction.InputName, variables);
            var filePath = VariableReplacer.ReplaceVariables(mediaFileAction.FilePath, variables);

            try
            {
                connection.Execute(obs =>
                {
                    // ffmpeg_source uses "local_file" for local files
                    var settings = new JObject { { "local_file", filePath } };
                    obs.SetInputSettings(inputName, settings, overlay: true);
                });

                _logger.LogInformation("Set media source '{Input}' file to '{File}' in OBS connection '{Connection}'",
                    inputName, filePath, connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting file of media source '{Input}' in OBS connection '{Connection}'",
                    inputName, connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
