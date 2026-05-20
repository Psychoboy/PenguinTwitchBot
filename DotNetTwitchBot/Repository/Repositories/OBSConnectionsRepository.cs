using DotNetTwitchBot.Bot.Models.Obs;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class OBSConnectionsRepository(ApplicationDbContext context) : GenericRepository<OBSConnection>(context)
    {
    }
}
