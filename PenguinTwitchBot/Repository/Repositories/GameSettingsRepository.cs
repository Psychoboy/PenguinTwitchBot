using PenguinTwitchBot.Bot.Models.Games;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class GameSettingsRepository(ApplicationDbContext context) : GenericRepository<GameSetting>(context), IGameSettingsRepository
    {
    }
}
