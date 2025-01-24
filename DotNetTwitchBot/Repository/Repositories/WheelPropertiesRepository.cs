using DotNetTwitchBot.Bot.Models.Wheel;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class WheelPropertiesRepository(ApplicationDbContext context) : GenericRepository<WheelProperty>(context), IWheelPropertiesRepository
    {
    }
}
