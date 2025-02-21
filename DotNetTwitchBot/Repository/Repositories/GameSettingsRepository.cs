using DotNetTwitchBot.Bot.Models.Games;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class GameSettingsRepository(ApplicationDbContext context) : GenericRepository<GameSetting>(context), IGameSettingsRepository
    {
    }
}
