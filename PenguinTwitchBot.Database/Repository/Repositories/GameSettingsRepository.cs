using PenguinTwitchBot.Database.Bot.Models.Games;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class GameSettingsRepository(ApplicationDbContext context) : GenericRepository<GameSetting>(context), IGameSettingsRepository
    {
    }
}
