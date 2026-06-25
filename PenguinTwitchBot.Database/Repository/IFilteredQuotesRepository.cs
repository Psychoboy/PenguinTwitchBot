using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IFilteredQuotesRepository : IGenericRepository<FilteredQuoteType>
    {
    }
}
