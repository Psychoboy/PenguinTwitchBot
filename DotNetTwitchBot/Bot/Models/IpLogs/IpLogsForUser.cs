namespace DotNetTwitchBot.Bot.Models.IpLogs
{
    public class IpLogsForUser
    {
        public string Username { get; set; } = null!;
        public string Ip { get; set; } = null!;
        public int Count { get; set; }
        public DateTime LastUsed { get; set; }
    }
}
