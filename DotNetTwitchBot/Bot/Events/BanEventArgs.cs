namespace DotNetTwitchBot.Bot.Events
{
    public class BanEventArgs
    {
        public string Name { get; set; } = null!;
        public bool IsUnBan { get; set; }
    }
}
