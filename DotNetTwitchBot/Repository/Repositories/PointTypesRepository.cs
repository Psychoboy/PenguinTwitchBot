using DotNetTwitchBot.Bot.Models.Points;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class PointTypesRepository(ApplicationDbContext context) : 
        GenericRepository<PointType>(context), IPointTypesRepository
    {
    }
}
