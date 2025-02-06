using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models
{
    public class Song
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public string SongId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string RequestedBy { get; set; } = "DJ Waffle";
        public TimeSpan Duration { get; set; }

        public Song CreateDeepCopy()
        {
            return new Song { Id = Id, Title = Title, RequestedBy = RequestedBy, Duration = Duration, SongId = SongId };
        }
    }
}