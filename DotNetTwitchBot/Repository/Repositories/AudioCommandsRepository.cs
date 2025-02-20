using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class AudioCommandsRepository : GenericRepository<AudioCommand>, IAudioCommandsRepository
    {
        public AudioCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
