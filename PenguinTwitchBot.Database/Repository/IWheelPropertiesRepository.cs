using PenguinTwitchBot.Database.Bot.Models.Wheel;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IWheelPropertiesRepository : IGenericRepository<WheelProperty>
    {
    }
}
