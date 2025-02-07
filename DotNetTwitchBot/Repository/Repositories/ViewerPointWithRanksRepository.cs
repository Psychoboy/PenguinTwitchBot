namespace DotNetTwitchBot.Repository.Repositories
{
    public class ViewerPointWithRanksRepository : GenericRepository<ViewerPointWithRank>, IViewerPointWithRanksRepository
    {
        public ViewerPointWithRanksRepository(ApplicationDbContext context) : base(context)
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
