using PenguinTwitchBot.Bot.Models.Commands;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class CooldownsRepository(ApplicationDbContext context) : GenericRepository<CurrentCooldowns>(context), ICooldownsRepository
    {
    }
}
