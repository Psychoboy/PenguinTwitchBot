using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class ActionKeywordsRepository : GenericRepository<ActionKeyword>, IActionKeywordsRepository
    {
        public ActionKeywordsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
