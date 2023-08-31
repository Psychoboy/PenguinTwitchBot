namespace DotNetTwitchBot.Bot.Events
{
    public class BanEventArgs
    {
        public string Name { get; set; }
        public bool IsUnBan { get; set; }
    }
}
