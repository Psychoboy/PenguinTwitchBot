using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using Microsoft.EntityFrameworkCore;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class SubActionsRepository : GenericRepository<SubActionType>, ISubActionsRepository
    {
        public SubActionsRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<int> GetNextIdAsync()
        {
            var maxId = await _context.SubActions.MaxAsync(s => (int?)s.Id) ?? 0;
            return maxId + 1;
        }
    }
}
