using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IActionKeywordsRepository : IGenericRepository<ActionKeyword>
    {
    }
}
