using PenguinTwitchBot.Database.Bot.Models.Games;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IGameSettingsRepository : IGenericRepository<GameSetting>
    {
    }
}
