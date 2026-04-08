using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.ObsConnector;
using OBSWebsocketDotNet.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsTriggerHotkeyHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsTriggerHotkeyHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsTriggerHotkey;

        public ObsTriggerHotkeyHandler(
            IOBSConnectionManager connectionManager,
            ILogger<ObsTriggerHotkeyHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not ObsTriggerHotkeyType hotkeyType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type ObsTriggerHotkey is not of ObsTriggerHotkeyType class");
            }

            if (!hotkeyType.OBSConnectionId.HasValue)
            {
                throw new SubActionHandlerException(subAction, "OBSTriggerHotkey SubAction missing connection ID");
            }

            var connection = _connectionManager.GetManagedConnection(hotkeyType.OBSConnectionId.Value) ?? throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", hotkeyType.OBSConnectionId.Value);
            if (!connection.IsConnected)
            {
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);
            }

            // Replace variables in hotkey name
            var hotkeyName = VariableReplacer.ReplaceVariables(hotkeyType.HotkeyName, variables);

            // Try to parse as OBSHotkey enum, fallback to string
            if (Enum.TryParse<OBSHotkey>(hotkeyName, out var obsHotkey))
            {
                connection.Execute(obs => obs.TriggerHotkeyByKeySequence(obsHotkey));
                _logger.LogDebug("Triggered OBS hotkey {Hotkey} on connection '{Connection}'", 
                    hotkeyName, connection.Name);
            }
            else
            {
                connection.Execute(obs => obs.TriggerHotkeyByName(hotkeyName));
                _logger.LogDebug("Triggered OBS hotkey '{Hotkey}' on connection '{Connection}'", 
                    hotkeyName, connection.Name);
            }

            return Task.CompletedTask;
        }
    }
}
