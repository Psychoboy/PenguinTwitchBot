using PenguinTwitchBot.Database.Bot.Models.Timers;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface ITimerGroupsRepository : IGenericRepository<TimerGroup>
    {
    }
}
