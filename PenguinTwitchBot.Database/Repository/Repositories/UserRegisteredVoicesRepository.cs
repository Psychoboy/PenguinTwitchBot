
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class UserRegisteredVoicesRepository : GenericRepository<UserRegisteredVoice>, IUserRegisteredVoicesRepository
    {
        public UserRegisteredVoicesRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
