using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Models.Games;
using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Games
{
    public class GameSettingsService(
        ILogger<GameSettingsService> logger,
        IServiceScopeFactory scopeFactory
        ) : IGameSettingsService
    {
        public async Task<string> GetStringSetting(string gameName, string settingName, string defaultValue)
        {
            var setting = await GetSetting(gameName, settingName);
            if (setting != null)
            {
                if (setting.SettingStringValue != null)
                {
                    return setting.SettingStringValue;
                }
            }
            return defaultValue;
        }
        public async Task<List<string>> GetStringListSetting(string gameName, string settingName, List<string> defaultValue)
        {
            var setting = await GetSetting(gameName, settingName);
            if (setting != null)
            {
                if (setting.SettingStringValue != null)
                {
                    return [.. setting.SettingStringValue.Split(',').Select(x => x.Trim())];
                }
            }
            return defaultValue;
        }

        public async Task<int> GetIntSetting(string gameName, string settingName, int defaultValue)
        {
            var setting = await GetSetting(gameName, settingName);
            if (setting != null)
            {
                return setting.SettingIntValue;
            }
            return defaultValue;
        }

        public async Task<bool> GetBoolSetting(string gameName, string settingName, bool defaultValue)
        {
            var setting = await GetSetting(gameName, settingName);
            if (setting != null)
            {
                return setting.SettingBoolValue;
            }
            return defaultValue;
        }

        public async Task<double> GetDoubleSetting(string gameName, string settingName, double defaultValue)
        {
            var setting = await GetSetting(gameName, settingName);
            if (setting != null)
            {
                return setting.SettingDoubleValue;
            }
            return defaultValue;
        }

        public async Task SetStringSetting(string gameName, string settingName, string value)
        {
            var setting = await GetSetting(gameName, settingName);
            setting ??= new GameSetting
            {
                GameName = gameName,
                SettingName = settingName,
                SettingStringValue = value
            };
            setting.SettingStringValue = value;
            await SaveSetting(setting);
        }

        public async Task SetIntSetting(string gameName, string settingName, int value)
        {
            var setting = await GetSetting(gameName, settingName);
            setting ??= new GameSetting
            {
                GameName = gameName,
                SettingName = settingName,
                SettingIntValue = value
            };
            setting.SettingIntValue = value;
            await SaveSetting(setting);
        }

        public async Task SetBoolSetting(string gameName, string settingName, bool value)
        {
            var setting = await GetSetting(gameName, settingName);
            setting ??= new GameSetting
            {
                GameName = gameName,
                SettingName = settingName,
                SettingBoolValue = value
            };
            setting.SettingBoolValue = value;
            await SaveSetting(setting);
        }

        public async Task SetDoubleSetting(string gameName, string settingName, double value)
        {
            var setting = await GetSetting(gameName, settingName);
            setting ??= new GameSetting
            {
                GameName = gameName,
                SettingName = settingName,
                SettingDoubleValue = value
            };
            setting.SettingDoubleValue = value;
            await SaveSetting(setting);
        }


        private async Task<GameSetting?> GetSetting(string gameName, string settingName)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await dbContext.GameSettings.GetAsync(x => x.GameName.Equals(gameName) && x.SettingName.Equals(settingName))).FirstOrDefault();
        }

        private async Task SaveSetting(GameSetting setting)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            dbContext.GameSettings.Update(setting);
            await dbContext.SaveChangesAsync();
        }

        public async Task<PointType> GetPointTypeForGame(string gameName)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await dbContext.GameSettings.GetAsync(x => x.GameName.Equals(gameName) && x.SettingName.Equals("PointTypeId"))).FirstOrDefault();
            if(setting != null)
            {
                var pointType = await dbContext.PointTypes.GetByIdAsync(setting.SettingIntValue);
                if(pointType != null)
                    return pointType;
            }
            logger.LogWarning("PointType not found for game {gameName}, using default.", gameName);
            await SetPointTypeForGame(gameName, 1);
            return PointsSystem.GetDefaultPointType();
        }

        public async Task<List<PointGamePair>> GetAllPointTypes()
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var settings = await dbContext.GameSettings.Find(x => x.SettingName.Equals("PointTypeId")).ToListAsync();
            var defaultPointType = await dbContext.PointTypes.Find(x => x.Id == 1).FirstAsync();
            List<PointGamePair> result = [];
            foreach (var setting in settings)
            {
                var pointType = await dbContext.PointTypes.GetByIdAsync(setting.SettingIntValue);
                if(pointType == null)
                {
                    await RegisterDefaultPointForGame(setting.GameName);
                    result.Add(new PointGamePair
                    {
                        Setting = setting,
                        PointType = defaultPointType
                    });
                } else
                {
                    result.Add(new PointGamePair { Setting = setting, PointType = pointType });
                }
            }
            result.Sort((a, b) => a.Setting.GameName.CompareTo(b.Setting.GameName));
            return result;
        }

        public async Task RegisterDefaultPointForGame(string gameName)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await dbContext.GameSettings.GetAsync(x => x.GameName.Equals(gameName) && x.SettingName.Equals("PointTypeId"))).FirstOrDefault();
            if (setting == null)
            {
                await SetPointTypeForGame(gameName, 1);
            }
        }

        public async Task SetPointTypeForGame(string gameName, int pointTypeId)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await dbContext.GameSettings.GetAsync(x => x.GameName.Equals(gameName) && x.SettingName.Equals("PointTypeId"))).FirstOrDefault();
            if (setting != null)
            {
                setting.SettingIntValue = pointTypeId;
            }
            else
            {
                setting = new GameSetting
                {
                    GameName = gameName,
                    SettingName = "PointTypeId",
                    SettingIntValue = pointTypeId
                };
            }
            dbContext.GameSettings.Update(setting);
            await dbContext.SaveChangesAsync();
        }
    }
}
