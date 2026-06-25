using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class CooldownsRepository(ApplicationDbContext context) : GenericRepository<CurrentCooldowns>(context), ICooldownsRepository
    {
    }
}
