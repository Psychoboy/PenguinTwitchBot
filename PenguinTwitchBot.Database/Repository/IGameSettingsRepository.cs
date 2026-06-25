using PenguinTwitchBot.Bot.Models.Games;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IGameSettingsRepository : IGenericRepository<GameSetting>
    {
    }
}
