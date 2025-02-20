using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class KeywordsRepository : GenericRepository<KeywordType>, IKeywordsRepository
    {
        public KeywordsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
