using PenguinTwitchBot.Bot.Models.Timers;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface ITimerGroupsRepository : IGenericRepository<TimerGroup>
    {
    }
}
