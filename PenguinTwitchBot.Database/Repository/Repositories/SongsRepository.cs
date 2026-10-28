namespace PenguinTwitchBot.Database.Repository.Repositories
{
    public class SongsRepository(ApplicationDbContext context) : GenericRepository<Song>(context), ISongsRepository
    {
    }
}
