using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IActionKeywordsRepository : IGenericRepository<ActionKeyword>
    {
    }
}
