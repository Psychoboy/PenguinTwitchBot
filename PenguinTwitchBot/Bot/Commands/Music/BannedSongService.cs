using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Helpers;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Commands.Music
{
    public class BannedSongService(
        IServiceScopeFactory scopeFactory,
        ILogger<BannedSongService> logger) : IBannedSongService
    {
        public const string BannedSongRequestTriggerName = "Song.BannedRequest";

        public async Task<List<BannedSong>> GetBannedSongsAsync()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.BannedSongs.GetAllOrderedAsync();
        }

        public async Task<BannedSong?> GetBannedSongAsync(string songIdOrUrl)
        {
            var songId = YouTubeUrlHelper.ExtractVideoId(songIdOrUrl);
            if (string.IsNullOrWhiteSpace(songId)) return null;

            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.BannedSongs.GetBySongIdAsync(songId);
        }

        public async Task<bool> IsBannedAsync(string songIdOrUrl)
        {
            return await GetBannedSongAsync(songIdOrUrl) != null;
        }

        public async Task<BannedSong?> BanSongAsync(string songIdOrUrl, string? title, string reason, string bannedBy)
        {
            var songId = YouTubeUrlHelper.ExtractVideoId(songIdOrUrl);
            if (string.IsNullOrWhiteSpace(songId)) return null;

            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var existing = await unitOfWork.BannedSongs.GetBySongIdAsync(songId);
            if (existing != null) return existing;

            var bannedSong = new BannedSong
            {
                SongId = songId,
                Title = Clamp(title, 512),
                Reason = Clamp(reason, 512),
                BannedBy = Clamp(bannedBy, 128),
                BannedAt = DateTime.UtcNow
            };

            await unitOfWork.BannedSongs.AddAsync(bannedSong);
            await unitOfWork.SaveChangesAsync();
            return bannedSong;
        }

        private static string Clamp(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        public async Task<bool> UnbanSongAsync(int id)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var bannedSong = await unitOfWork.BannedSongs.GetByIdAsync(id);
            if (bannedSong == null) return false;

            unitOfWork.BannedSongs.Remove(bannedSong);
            await unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task RaiseBannedSongRequestedAsync(BannedSong bannedSong, string requestedBy, string requestedInput)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameEnabledAsync(
                    TriggerTypes.BannedSongRequest,
                    BannedSongRequestTriggerName);

                if (actions.Count == 0) return;

                var variables = new ConcurrentDictionary<string, string>
                {
                    ["user"] = requestedBy,
                    ["displayname"] = requestedBy,
                    ["banned_song_id"] = bannedSong.SongId,
                    ["banned_song_title"] = bannedSong.Title,
                    ["banned_song_reason"] = bannedSong.Reason,
                    ["banned_song_banned_by"] = bannedSong.BannedBy,
                    ["banned_song_url"] = $"https://youtu.be/{bannedSong.SongId}",
                    ["banned_song_request"] = requestedInput
                };

                foreach (var action in actions)
                {
                    await actionService.EnqueueAction(new ConcurrentDictionary<string, string>(variables), action);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed raising banned song request trigger for {SongId}", bannedSong.SongId);
            }
        }
    }
}
