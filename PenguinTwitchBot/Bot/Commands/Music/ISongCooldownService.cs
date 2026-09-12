using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Bot.Commands.Music
{
    public class SongCooldownSettings
    {
        public bool Enabled { get; set; } = false;
        public int CooldownMinutes { get; set; } = 60;
        public bool MessageEnabled { get; set; } = false;
        public string Message { get; set; } = "Song '{0}' is on cooldown for another {1}.";
        public bool ExemptSkippedVetoed { get; set; } = false;
    }

    public interface ISongCooldownService
    {
        Task<SongCooldownSettings> GetSettingsAsync();
        Task SaveSettingsAsync(SongCooldownSettings settings);

        Task<bool> IsOnCooldownAsync(string songIdOrUrl);
        Task<SongCooldown?> GetActiveCooldownAsync(string songIdOrUrl);
        Task<List<SongCooldown>> GetActiveCooldownsAsync();

        Task<SongCooldown?> AddCooldownAsync(string songIdOrUrl, string? title, TimeSpan duration, string addedBy = "System");
        Task<bool> ClearCooldownAsync(string songIdOrUrl);
        Task ClearAllCooldownsAsync();
    }
}
