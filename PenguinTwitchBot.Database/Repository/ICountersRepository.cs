using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface ICountersRepository : IGenericRepository<Counter>
    {
    }
}
