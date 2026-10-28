using PenguinTwitchBot.Database.Bot.Models.Wheel;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class WheelPropertiesRepository(ApplicationDbContext context) : GenericRepository<WheelProperty>(context), IWheelPropertiesRepository
    {
    }
}
