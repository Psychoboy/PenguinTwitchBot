namespace DotNetTwitchBot.Bot.Models.IpLogs
{
    public class IpLogEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string Ip { get; set; } = null!;
        public int Count { get; set; } = 1;
        public DateTime ConnectedDate { get; set; } = DateTime.Now;
    }
}
