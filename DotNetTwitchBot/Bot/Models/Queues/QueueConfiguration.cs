namespace DotNetTwitchBot.Bot.Models.Queues
{
    public class QueueConfiguration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public string Name { get; set; } = null!;
        
        public bool IsBlocking { get; set; } = true;
        
        public bool Enabled { get; set; } = true;
        
        public int MaxConcurrentActions { get; set; } = 50;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
