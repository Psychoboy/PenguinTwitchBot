using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface ICooldownsRepository : IGenericRepository<CurrentCooldowns>
    {
    }
}
