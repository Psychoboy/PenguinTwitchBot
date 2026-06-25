using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IDeathCountersRepository : IGenericRepository<DeathCounter>
    {
    }
}
