namespace DotNetTwitchBot.Repository.Repositories
{
    public class SongsRepository(ApplicationDbContext context) : GenericRepository<Song>(context), ISongsRepository
    {
        public override Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }

        public override Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }
    }
}
