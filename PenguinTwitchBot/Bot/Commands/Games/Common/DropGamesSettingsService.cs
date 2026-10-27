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
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<DropGamesSettings> GetSettingsAsync()
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            await _lock.WaitAsync();
            try
            {
                if (_cachedSettings != null)
                {
                    return _cachedSettings;
                }

                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
                if (unitOfWork != null)
                {
                    var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingsKey)).FirstOrDefault();
                    if (setting == null)
                    {
                        // Successful read finds no settings row: initialize and persist defaults
                        var defaults = new DropGamesSettings();
                        EnsureStockThemes(defaults);
                        await PersistSettingsToDbAsync(defaults);
                        _cachedSettings = defaults;
                        return _cachedSettings;
                    }

                    if (!string.IsNullOrWhiteSpace(setting.StringSetting))
                    {
                        var loaded = JsonSerializer.Deserialize<DropGamesSettings>(setting.StringSetting, _jsonOptions);
                        if (loaded != null)
                        {
                            EnsureStockThemes(loaded);
                            _cachedSettings = loaded;
                            return _cachedSettings;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load DropGamesSettings from database; using in-memory defaults.");
            }
            finally
            {
                _lock.Release();
            }

            // Database read or deserialization failure: return in-memory defaults without caching or persisting
            var fallback = new DropGamesSettings();
            EnsureStockThemes(fallback);
            return fallback;
        }

        public async Task SaveSettingsAsync(DropGamesSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            EnsureStockThemes(settings);

            await _lock.WaitAsync();
            try
            {
                DropGameMediaResolver.ClearPreparedThemeCache();
                await PersistSettingsToDbAsync(settings);
                _cachedSettings = settings;
            }
            catch
            {
                _cachedSettings = null;
                throw;
            }
            finally
            {
                _lock.Release();
            }
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
                logger.LogError(ex, "Failed to save DropGamesSettings to database.");
                throw;
            }
        }

        public static void EnsureStockThemes(DropGamesSettings settings)
        {
            settings.Parachute ??= new ParachuteGameSettings();
            settings.Plinko ??= new PlinkoGameSettings();
            settings.Balloon ??= new BalloonAscentGameSettings();
            settings.Curling ??= new CurlingGameSettings();
            settings.Golf ??= new GolfGameSettings();
            settings.Racing ??= new RacingGameSettings();
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
            else if (IsRecognizedLegacySevenBucketLayout(settings.Plinko.Buckets))
            {
                settings.Plinko.Buckets = PlinkoGameSettings.CreateDefaultBuckets();
            }

            foreach (var theme in settings.Themes)
            {
                if (string.IsNullOrWhiteSpace(theme.LandingParticleType))
                {
                    theme.LandingParticleType = theme.SeasonPreset.ToLowerInvariant() switch
                    {
                        "summer" => "splash",
                        "fall" => "leaves",
                        "winter" => "snow",
                        "halloween" => "candies",
                        "christmas" => "confetti",
                        _ => "petals"
                    };
                    theme.EnableLandingParticles = true;
                    theme.LandingParticleDensity = 36;
                }
            }

            if (settings.Parachute.DefaultDropletRandomSizes == null || settings.Parachute.DefaultDropletRandomSizes.Count == 0)
            {
                settings.Parachute.DefaultDropletRandomSizes = ["Small", "Medium", "Large", "Giant"];
            }
        }

        private static bool IsRecognizedLegacySevenBucketLayout(List<PlinkoBucket> buckets)
        {
            if (buckets.Count != 7) return false;

            return buckets[0].Label == "MISS" && buckets[0].Score == 0 && !buckets[0].IsWin &&
                   buckets[1].Label == "50 PTS" && buckets[1].Score == 50 && buckets[1].IsWin &&
                   buckets[2].Label == "100 PTS" && buckets[2].Score == 100 && buckets[2].IsWin &&
                   buckets[3].Label == "JACKPOT" && buckets[3].Score == 500 && buckets[3].IsWin &&
                   buckets[4].Label == "100 PTS" && buckets[4].Score == 100 && buckets[4].IsWin &&
                   buckets[5].Label == "50 PTS" && buckets[5].Score == 50 && buckets[5].IsWin &&
                   buckets[6].Label == "MISS" && buckets[6].Score == 0 && !buckets[6].IsWin;
        }
    }
}

