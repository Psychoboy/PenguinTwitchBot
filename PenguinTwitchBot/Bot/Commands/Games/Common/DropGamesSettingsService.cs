using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public class DropGamesSettingsService(
        IServiceScopeFactory scopeFactory,
        ILogger<DropGamesSettingsService> logger) : IDropGamesSettingsService
    {
        public const string SettingsKey = "drop_games.settings.v1";

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        private DropGamesSettings? _cachedSettings;

        public async Task<DropGamesSettings> GetSettingsAsync()
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            DropGamesSettings? loaded = null;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
                if (unitOfWork != null)
                {
                    var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingsKey)).FirstOrDefault();
                    if (setting != null && !string.IsNullOrWhiteSpace(setting.StringSetting))
                    {
                        loaded = JsonSerializer.Deserialize<DropGamesSettings>(setting.StringSetting, _jsonOptions);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load DropGamesSettings from database; using defaults.");
            }

            if (loaded == null)
            {
                loaded = new DropGamesSettings();
                EnsureStockThemes(loaded);
                await PersistSettingsToDbAsync(loaded);
            }
            else
            {
                EnsureStockThemes(loaded);
            }

            _cachedSettings = loaded;
            return _cachedSettings;
        }

        public async Task SaveSettingsAsync(DropGamesSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            EnsureStockThemes(settings);
            _cachedSettings = settings;
            await PersistSettingsToDbAsync(settings);
        }

        private async Task PersistSettingsToDbAsync(DropGamesSettings settings)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
                if (unitOfWork == null)
                {
                    return;
                }

                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                var existing = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingsKey)).FirstOrDefault();
                if (existing == null)
                {
                    await unitOfWork.Settings.AddAsync(new Setting
                    {
                        Name = SettingsKey,
                        DataType = Setting.DataTypeEnum.String,
                        StringSetting = json
                    });
                }
                else
                {
                    existing.DataType = Setting.DataTypeEnum.String;
                    existing.StringSetting = json;
                    unitOfWork.Settings.Update(existing);
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to save DropGamesSettings to database.");
            }
        }

        public static void EnsureStockThemes(DropGamesSettings settings)
        {
            settings.Themes ??= [];
            if (settings.Themes.Count > 0)
            {
                settings.Themes = settings.Themes
                    .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .ToList();
            }

            var stockDefaults = DropGameTheme.CreateDefaultSeasonalThemes();
            foreach (var stock in stockDefaults)
            {
                if (!settings.Themes.Any(t => t.Id.Equals(stock.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.Themes.Add(stock);
                }
            }

            if (settings.Plinko.Buckets == null || settings.Plinko.Buckets.Count == 0)
            {
                settings.Plinko.Buckets = PlinkoGameSettings.CreateDefaultBuckets();
            }
            else if (settings.Plinko.Buckets.Count >= 14 &&
                     settings.Plinko.Buckets.Count % 7 == 0 &&
                     settings.Plinko.Buckets[0].Label == "MISS" &&
                     settings.Plinko.Buckets[1].Label == "50 PTS" &&
                     settings.Plinko.Buckets[1].IsWin &&
                     settings.Plinko.Buckets[3].Label == "JACKPOT")
            {
                settings.Plinko.Buckets = settings.Plinko.Buckets.Skip(settings.Plinko.Buckets.Count - 7).ToList();
            }
        }
    }
}

