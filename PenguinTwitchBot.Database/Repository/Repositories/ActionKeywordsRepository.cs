using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class ActionKeywordsRepository : GenericRepository<ActionKeyword>, IActionKeywordsRepository
    {
        public ActionKeywordsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
