using PenguinTwitchBot.Bot.Models.Games;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class GameSettingsRepository(ApplicationDbContext context) : GenericRepository<GameSetting>(context), IGameSettingsRepository
    {
    }
}
