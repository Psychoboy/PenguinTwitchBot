using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IKnownBotsRepository : IGenericRepository<KnownBot>
    {
    }
}
