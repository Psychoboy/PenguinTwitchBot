namespace DotNetTwitchBot.Repository.Repositories
{
    public class ViewerMessageCountsWithRankRepository : GenericRepository<ViewerMessageCountWithRank>, IViewerMessageCountsWithRankRepository
    {
        public ViewerMessageCountsWithRankRepository(ApplicationDbContext context) : base(context)
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
