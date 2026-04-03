namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Stream Uptime",
        description: "Get how long the stream has been live",
        icon: MdiIcons.Timer,
        color: "Default",
        tableName: "subactions_uptime")]
    public class UptimeType : SimpleSubActionType
    {
        public UptimeType()
        {
            SubActionTypes = SubActionTypes.Uptime;
        }

        protected override string TextLabel => "Format string (e.g., HH:mm:ss)";
        protected override string TextHelperText => "Use time format strings. Variables: %user%, %target%, etc.";
    }
}
