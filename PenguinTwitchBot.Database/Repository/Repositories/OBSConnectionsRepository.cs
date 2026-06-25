using PenguinTwitchBot.Bot.Models.Obs;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class OBSConnectionsRepository(ApplicationDbContext context) : GenericRepository<OBSConnection>(context)
    {
    }
}
