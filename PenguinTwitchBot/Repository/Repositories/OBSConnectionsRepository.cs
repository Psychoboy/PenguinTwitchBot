using PenguinTwitchBot.Bot.Models.Obs;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class OBSConnectionsRepository(ApplicationDbContext context) : GenericRepository<OBSConnection>(context)
    {
    }
}
