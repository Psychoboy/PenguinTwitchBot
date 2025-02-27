namespace DotNetTwitchBot.Repository.Repositories
{
    public class SongRequestViewItemsRepository : GenericRepository<SongRequestViewItem>, ISongRequestViewItemsRepository
    {
        public SongRequestViewItemsRepository(ApplicationDbContext context) : base(context)
        {
        }

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
