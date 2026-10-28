using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class PointCommandsRepository(ApplicationDbContext context) : GenericRepository<PointCommand>(context), IPointCommandsRepository
    {
    }
}
