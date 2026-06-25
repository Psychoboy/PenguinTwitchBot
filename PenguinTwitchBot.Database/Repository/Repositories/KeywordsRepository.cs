using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class KeywordsRepository : GenericRepository<KeywordType>, IKeywordsRepository
    {
        public KeywordsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
