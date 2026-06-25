


using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class AliasRepository : GenericRepository<AliasModel>, IAliasRepository
    {
        public AliasRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
