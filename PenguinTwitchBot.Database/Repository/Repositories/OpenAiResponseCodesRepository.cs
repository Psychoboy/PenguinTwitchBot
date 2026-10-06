using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class OpenAiResponseCodesRepository : GenericRepository<OpenAiResponseCode>, IOpenAiResponseCodesRepository
    {
        public OpenAiResponseCodesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

