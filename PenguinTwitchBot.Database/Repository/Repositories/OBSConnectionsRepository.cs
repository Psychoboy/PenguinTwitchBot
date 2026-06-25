using PenguinTwitchBot.Database.Bot.Models.Obs;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class OBSConnectionsRepository(ApplicationDbContext context) : GenericRepository<OBSConnection>(context)
    {
    }
}
