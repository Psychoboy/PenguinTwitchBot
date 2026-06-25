using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IQuotesRepository : IGenericRepository<QuoteType>
    {
    }
}
