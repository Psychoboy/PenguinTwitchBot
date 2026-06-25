using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class ScAiResponsesRepository : GenericRepository<ScAiResponseCodes>, IScAiResponsesRepository
    {
        public ScAiResponsesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
