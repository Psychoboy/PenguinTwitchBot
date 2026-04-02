namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    public class WriteFileType : SubActionType
    {
        public WriteFileType()
        {
            SubActionTypes = SubActionTypes.WriteFile;
        }

        public bool Append { get; set; } = true;
    }
}
