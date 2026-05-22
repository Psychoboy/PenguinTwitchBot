using PenguinTwitchBot.Bot.Models.Commands;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class AudioCommandsRepository : GenericRepository<AudioCommand>, IAudioCommandsRepository
    {
        public AudioCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
