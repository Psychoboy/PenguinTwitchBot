using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class PointTypesRepository(ApplicationDbContext context) : 
        GenericRepository<PointType>(context), IPointTypesRepository
    {
    }
}
