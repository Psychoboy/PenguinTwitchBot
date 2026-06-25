using PenguinTwitchBot.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository.Repositories
{
    public class AudioCommandsRepository : GenericRepository<AudioCommand>, IAudioCommandsRepository
    {
        public AudioCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
