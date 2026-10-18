namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    public interface IDropGamesSettingsService
    {
        Task<DropGamesSettings> GetSettingsAsync();
        Task SaveSettingsAsync(DropGamesSettings settings);
    }
}

