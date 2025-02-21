using DotNetTwitchBot.Bot.Models.Games;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class GameSettingsService(
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
                    return setting.SettingStringValue.Split(',').Select(x => x.Trim()).ToList();
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
    }
}
