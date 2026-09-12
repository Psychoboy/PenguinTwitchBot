using Google.Apis.YouTube.v3;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Helpers;

namespace PenguinTwitchBot.Bot.Commands.Music
{
    public class SongCooldownService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SongCooldownService> logger) : ISongCooldownService
    {
        private const string SettingEnabled = "SongCooldown.Enabled";
        private const string SettingMinutes = "SongCooldown.Minutes";
        private const string SettingMessageEnabled = "SongCooldown.MessageEnabled";
        private const string SettingMessage = "SongCooldown.Message";
        private const string SettingExemptSkippedVetoed = "SongCooldown.ExemptSkippedVetoed";

        public async Task<SongCooldownSettings> GetSettingsAsync()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var enabled = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingEnabled)).FirstOrDefault()?.IntSetting == 1;
            var minutes = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingMinutes)).FirstOrDefault()?.IntSetting ?? 60;
            var msgEnabled = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingMessageEnabled)).FirstOrDefault()?.IntSetting == 1;
            var msg = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingMessage)).FirstOrDefault()?.StringSetting;
            var exempt = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingExemptSkippedVetoed)).FirstOrDefault()?.IntSetting == 1;

            if (string.IsNullOrWhiteSpace(msg))
            {
                msg = "Song '{0}' is on cooldown for another {1}.";
            }

            return new SongCooldownSettings
            {
                Enabled = enabled,
                CooldownMinutes = minutes > 0 ? minutes : 60,
                MessageEnabled = msgEnabled,
                Message = msg,
                ExemptSkippedVetoed = exempt
            };
        }

        public async Task SaveSettingsAsync(SongCooldownSettings settings)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await SaveIntSetting(unitOfWork, SettingEnabled, settings.Enabled ? 1 : 0);
            await SaveIntSetting(unitOfWork, SettingMinutes, settings.CooldownMinutes);
            await SaveIntSetting(unitOfWork, SettingMessageEnabled, settings.MessageEnabled ? 1 : 0);
            await SaveStringSetting(unitOfWork, SettingMessage, settings.Message ?? "");
            await SaveIntSetting(unitOfWork, SettingExemptSkippedVetoed, settings.ExemptSkippedVetoed ? 1 : 0);

            await unitOfWork.SaveChangesAsync();
        }

        private static async Task SaveIntSetting(IUnitOfWork unitOfWork, string name, int value)
        {
            var existing = (await unitOfWork.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
            if (existing == null)
            {
                await unitOfWork.Settings.AddAsync(new Setting { Name = name, DataType = Setting.DataTypeEnum.Int, IntSetting = value });
            }
            else
            {
                existing.DataType = Setting.DataTypeEnum.Int;
                existing.IntSetting = value;
                unitOfWork.Settings.Update(existing);
            }
        }

        private static async Task SaveStringSetting(IUnitOfWork unitOfWork, string name, string value)
        {
            var existing = (await unitOfWork.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
            if (existing == null)
            {
                await unitOfWork.Settings.AddAsync(new Setting { Name = name, DataType = Setting.DataTypeEnum.String, StringSetting = value });
            }
            else
            {
                existing.DataType = Setting.DataTypeEnum.String;
                existing.StringSetting = value;
                unitOfWork.Settings.Update(existing);
            }
        }

        public async Task<bool> IsOnCooldownAsync(string songIdOrUrl)
        {
            var songId = YouTubeUrlHelper.ExtractVideoId(songIdOrUrl) ?? songIdOrUrl;
            if (string.IsNullOrWhiteSpace(songId)) return false;

            var settings = await GetSettingsAsync();
            if (!settings.Enabled) return false;

            var active = await GetActiveCooldownAsync(songId);
            return active != null;
        }

        public async Task<SongCooldown?> GetActiveCooldownAsync(string songIdOrUrl)
        {
            var songId = YouTubeUrlHelper.ExtractVideoId(songIdOrUrl) ?? songIdOrUrl;
            if (string.IsNullOrWhiteSpace(songId)) return null;

            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.SongCooldowns.GetBySongIdAsync(songId);
        }

        public async Task<List<SongCooldown>> GetActiveCooldownsAsync()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.SongCooldowns.GetActiveCooldownsAsync();
        }

        public async Task<SongCooldown?> AddCooldownAsync(string songIdOrUrl, string? title, TimeSpan duration, string addedBy = "System")
        {
            var songId = YouTubeUrlHelper.ExtractVideoId(songIdOrUrl) ?? songIdOrUrl;
            if (string.IsNullOrWhiteSpace(songId)) return null;

            if (string.IsNullOrWhiteSpace(title) || title.Equals(songId, StringComparison.OrdinalIgnoreCase))
            {
                title = await ResolveSongTitleAsync(songId) ?? songId;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await unitOfWork.SongCooldowns.RemoveBySongIdAsync(songId);

            var expiresAt = DateTime.UtcNow.Add(duration);
            var cooldown = new SongCooldown
            {
                SongId = songId,
                Title = title,
                CooldownExpiresAt = expiresAt,
                AddedBy = addedBy,
                AddedAt = DateTime.UtcNow
            };

            await unitOfWork.SongCooldowns.AddAsync(cooldown);
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Added song cooldown for {SongId} ('{Title}') expiring at {ExpiresAt} by {AddedBy}", songId, cooldown.Title, expiresAt, addedBy);
            return cooldown;
        }

        private async Task<string?> ResolveSongTitleAsync(string songId)
        {
            try
            {
                var apiKey = configuration["youtubeApi"];
                if (string.IsNullOrWhiteSpace(apiKey)) return null;

                using var youtubeService = new YouTubeService(new Google.Apis.Services.BaseClientService.Initializer
                {
                    ApiKey = apiKey,
                    ApplicationName = GetType().ToString()
                });

                var request = youtubeService.Videos.List("snippet");
                request.Id = songId;
                var response = await request.ExecuteAsync();
                var item = response?.Items?.FirstOrDefault();
                return item?.Snippet?.Title;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve YouTube video title for {SongId}", songId);
                return null;
            }
        }

        public async Task<bool> ClearCooldownAsync(string songIdOrUrl)
        {
            var songId = YouTubeUrlHelper.ExtractVideoId(songIdOrUrl) ?? songIdOrUrl;
            if (string.IsNullOrWhiteSpace(songId)) return false;

            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await unitOfWork.SongCooldowns.RemoveBySongIdAsync(songId);
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Cleared song cooldown for {SongId}", songId);
            return true;
        }

        public async Task ClearAllCooldownsAsync()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await unitOfWork.SongCooldowns.ClearAllAsync();
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Cleared all song cooldowns.");
        }
    }
}
