using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IFilteredQuotesRepository : IGenericRepository<FilteredQuoteType>
    {
    }
}
