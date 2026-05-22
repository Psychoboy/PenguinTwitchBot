using PenguinTwitchBot.Bot.Models.Commands;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class KeywordsRepository : GenericRepository<KeywordType>, IKeywordsRepository
    {
        public KeywordsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
