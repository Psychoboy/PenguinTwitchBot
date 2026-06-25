using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class ActionKeywordsRepository : GenericRepository<ActionKeyword>, IActionKeywordsRepository
    {
        public ActionKeywordsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
