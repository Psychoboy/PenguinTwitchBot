using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class CooldownsRepository(ApplicationDbContext context) : GenericRepository<CurrentCooldowns>(context), ICooldownsRepository
    {
    }
}
