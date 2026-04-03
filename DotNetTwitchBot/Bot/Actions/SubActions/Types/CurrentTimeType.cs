namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Current Time",
        description: "Get the current time in a specified format",
        icon: "mdi-clock",
        color: "Default",
        tableName: "subactions_currenttime")]
    public class CurrentTimeType : SimpleSubActionType
    {
        public CurrentTimeType()
        {
            SubActionTypes = SubActionTypes.CurrentTime;
        }

        protected override string TextLabel => "Format string (e.g., HH:mm:ss)";
        protected override string TextHelperText => "Use date/time format strings. Variables: %user%, %target%, etc.";
    }
}
