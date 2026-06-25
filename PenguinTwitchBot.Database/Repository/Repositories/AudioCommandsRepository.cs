using PenguinTwitchBot.Database.Bot.Models.Commands;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class AudioCommandsRepository : GenericRepository<AudioCommand>, IAudioCommandsRepository
    {
        public AudioCommandsRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
