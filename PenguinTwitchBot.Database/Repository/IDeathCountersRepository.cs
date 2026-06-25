using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IDeathCountersRepository : IGenericRepository<DeathCounter>
    {
    }
}
