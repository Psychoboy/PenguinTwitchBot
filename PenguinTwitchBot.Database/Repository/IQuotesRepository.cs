using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IQuotesRepository : IGenericRepository<QuoteType>
    {
    }
}
