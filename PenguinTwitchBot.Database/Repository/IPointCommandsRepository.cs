using PenguinTwitchBot.Bot.Models.Points;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IPointCommandsRepository : IGenericRepository<PointCommand>
    {
    }
}
