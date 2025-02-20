using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class CooldownsRepository(ApplicationDbContext context) : GenericRepository<CurrentCooldowns>(context), ICooldownsRepository
    {
    }
}
