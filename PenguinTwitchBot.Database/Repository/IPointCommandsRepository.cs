using PenguinTwitchBot.Database.Bot.Models.Points;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IPointCommandsRepository : IGenericRepository<PointCommand>
    {
    }
}
