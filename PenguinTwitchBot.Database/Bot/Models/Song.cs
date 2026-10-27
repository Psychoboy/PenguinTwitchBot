using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.Models
{
    public class Song
    {
        [Key]
        public Guid Id { get; set; }
        public string SongId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string RequestedBy { get; set; } = "";
        public TimeSpan Duration { get; set; }

        public Guid MusicPlaylistId { get; set; }
        [JsonIgnore]
        public MusicPlaylist MusicPlaylist { get; set; } = null!;

        public Song CreateDeepCopy()
        {
            return new Song { Id = Id, Title = Title, RequestedBy = RequestedBy, Duration = Duration, SongId = SongId };
        }
    }
}