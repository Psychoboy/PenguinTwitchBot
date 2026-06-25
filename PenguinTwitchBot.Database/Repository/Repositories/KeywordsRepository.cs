using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class KeywordsRepository : GenericRepository<KeywordType>, IKeywordsRepository
    {
        public KeywordsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
