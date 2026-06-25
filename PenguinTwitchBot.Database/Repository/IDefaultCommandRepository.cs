using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IDefaultCommandRepository : IGenericRepository<DefaultCommand>
    {
    }
}
