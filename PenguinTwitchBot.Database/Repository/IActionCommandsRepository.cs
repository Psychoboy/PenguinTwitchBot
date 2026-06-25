using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IActionCommandsRepository : IGenericRepository<ActionCommand>
    {
    }
}
