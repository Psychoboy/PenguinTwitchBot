using PenguinTwitchBot.Bot.Models.Points;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IPointTypesRepository : IGenericRepository<PointType>
    {
    }
}
