using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Bot.Commands.Music
{
    public interface IBannedSongService
    {
        Task<List<BannedSong>> GetBannedSongsAsync();
        Task<BannedSong?> GetBannedSongAsync(string songIdOrUrl);
        Task<bool> IsBannedAsync(string songIdOrUrl);

        /// <summary>Bans a song. Returns null when the input could not be resolved to a YouTube video id.</summary>
        Task<BannedSong?> BanSongAsync(string songIdOrUrl, string? title, string reason, string bannedBy);

        Task<bool> UnbanSongAsync(int id);

        /// <summary>Runs any actions bound to the banned song request trigger.</summary>
        Task RaiseBannedSongRequestedAsync(BannedSong bannedSong, string requestedBy, string requestedInput);
    }
}
