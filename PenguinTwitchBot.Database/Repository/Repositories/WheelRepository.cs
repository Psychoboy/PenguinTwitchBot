using PenguinTwitchBot.Database.Bot.Models.Wheel;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class WheelRepository(ApplicationDbContext context) : GenericRepository<Wheel>(context), IWheelRepository
    {
    }
}
