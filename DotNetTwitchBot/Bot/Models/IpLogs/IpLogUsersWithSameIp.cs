namespace DotNetTwitchBot.Bot.Models.IpLogs
{
    public class IpLogUsersWithSameIp
    {
        public string User1 { get; set; } = null!;
        public string User2 { get; set;} = null!;
        public string Ip { get; set;} = null!;
        public int Count { get; set;}
    }
}
