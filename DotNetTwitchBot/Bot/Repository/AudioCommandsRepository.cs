namespace DotNetTwitchBot.Bot.Repository
{
    public class AudioCommandsRepository : GenericRepository<AudioCommand>, IAudioCommandsRepository
    {
        public AudioCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
