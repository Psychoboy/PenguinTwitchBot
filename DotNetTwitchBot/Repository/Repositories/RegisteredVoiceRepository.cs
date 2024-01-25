
namespace DotNetTwitchBot.Repository.Repositories
{
    public class RegisteredVoiceRepository : GenericRepository<RegisteredVoice>, IRegisteredVoiceRepository
    {
        public RegisteredVoiceRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
