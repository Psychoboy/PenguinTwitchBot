namespace PenguinTwitchBot.Bot.Events
{
    /// <summary>
    /// Fired when the broadcaster raids OUT to another channel (channel.raid
    /// where from_broadcaster is our channel).
    /// </summary>
    public class OutgoingRaidEventArgs
    {
        public string TargetUserId { get; set; } = string.Empty;
        public string TargetUserName { get; set; } = string.Empty;
        public string TargetDisplayName { get; set; } = string.Empty;
        public int NumberOfViewers { get; set; }
    }
}
