
using PenguinTwitchBot.Bot.DatabaseTools;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class AliasRepository : GenericRepository<AliasModel>, IAliasRepository
    {
        public AliasRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
