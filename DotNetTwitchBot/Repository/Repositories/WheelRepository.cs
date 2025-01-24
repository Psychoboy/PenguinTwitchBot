using DotNetTwitchBot.Bot.Models.Wheel;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class WheelRepository(ApplicationDbContext context) : GenericRepository<Wheel>(context), IWheelRepository
    {
    }
}
