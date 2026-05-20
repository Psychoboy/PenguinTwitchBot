using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.ObsConnector;
using DotNetTwitchBot.Bot.Queues;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Globalization;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ObsSetColorSourceColorHandler : ISubActionHandler
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<ObsSetColorSourceColorHandler> _logger;

        public SubActionTypes SupportedType => SubActionTypes.ObsSetColorSourceColor;

        public ObsSetColorSourceColorHandler(IOBSConnectionManager connectionManager, ILogger<ObsSetColorSourceColorHandler> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not ObsSetColorSourceColorType colorAction)
                throw new SubActionHandlerException(subAction, "SubAction with type ObsSetColorSourceColor is not of ObsSetColorSourceColorType class");

            if (!colorAction.OBSConnectionId.HasValue)
                throw new SubActionHandlerException(subAction, "ObsSetColorSourceColor SubAction missing connection ID");

            var connection = _connectionManager.GetManagedConnection(colorAction.OBSConnectionId.Value);
            if (connection == null)
                throw new SubActionHandlerException(subAction, "OBS connection with ID {Id} not found", colorAction.OBSConnectionId.Value);

            if (!connection.IsConnected)
                throw new SubActionHandlerException(subAction, "OBS connection '{Name}' is not connected", connection.Name);

            if (string.IsNullOrWhiteSpace(colorAction.InputName))
                throw new SubActionHandlerException(subAction, "Input Name is required for ObsSetColorSourceColor");

            var inputName = VariableReplacer.ReplaceVariables(colorAction.InputName, variables);
            var colorHex = VariableReplacer.ReplaceVariables(colorAction.Color, variables).TrimStart('#');

            if (!TryParseAbgrColor(colorHex, out var abgrColor))
                throw new SubActionHandlerException(subAction, "Invalid color format '{Color}'. Expected #RRGGBB or #AARRGGBB", colorAction.Color);

            try
            {
                connection.Execute(obs =>
                {
                    var settings = new JObject { { "color", (long)abgrColor } };
                    obs.SetInputSettings(inputName, settings, overlay: true);
                });

                _logger.LogInformation("Set color source '{Input}' color to '{Color}' in OBS connection '{Connection}'",
                    inputName, colorAction.Color, connection.Name);
            }
            catch (Exception ex)
            {
                throw new SubActionHandlerException(subAction, ex,
                    "Error setting color of color source '{Input}' in OBS connection '{Connection}'",
                    inputName, connection.Name);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Parses #RRGGBB or #AARRGGBB hex to OBS ABGR unsigned int.
        /// </summary>
        private static bool TryParseAbgrColor(string hexColor, out uint abgrColor)
        {
            abgrColor = 0;
            if (hexColor.Length == 6)
            {
                // #RRGGBB → alpha = 0xFF
                if (!uint.TryParse(hexColor, NumberStyles.HexNumber, null, out var rgb)) return false;
                byte r = (byte)((rgb >> 16) & 0xFF);
                byte g = (byte)((rgb >> 8) & 0xFF);
                byte b = (byte)(rgb & 0xFF);
                abgrColor = (0xFFu << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
                return true;
            }
            else if (hexColor.Length == 8)
            {
                // #AARRGGBB
                if (!uint.TryParse(hexColor, NumberStyles.HexNumber, null, out var argb)) return false;
                byte a = (byte)((argb >> 24) & 0xFF);
                byte r = (byte)((argb >> 16) & 0xFF);
                byte g = (byte)((argb >> 8) & 0xFF);
                byte b = (byte)(argb & 0xFF);
                abgrColor = ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
                return true;
            }
            return false;
        }
    }
}
