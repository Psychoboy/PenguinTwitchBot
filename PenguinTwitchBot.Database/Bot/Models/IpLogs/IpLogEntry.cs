namespace PenguinTwitchBot.Database.Bot.Models.IpLogs
{
    [ExcludeFromBackup]
    [Index(nameof(UserId), nameof(Ip))]
    [Index(nameof(UserId))]
    public class IpLogEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();
        public string Username { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string Ip { get; set; } = null!;
        public int Count { get; set; } = 1;
        public DateTime ConnectedDate { get; set; } = DateTime.UtcNow;
    }
}
