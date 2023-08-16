using DotNetTwitchBot.Bot.DataAccess;

namespace DotNetTwitchBot.Bot.Repository
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        
        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            AudioCommands = new AudioCommandsRepository(_context);
            DefaultCommands = new DefaultCommandRepository(_context);
            SongRequestMetrics = new SongRequestMetricsRepository(_context);
            RaidHistory = new RaidHistoryRepository(_context);
        }

        public IAudioCommandsRepository AudioCommands { get; private set; }
        public IDefaultCommandRepository DefaultCommands { get; private set; }
        public ISongRequestMetricsRepository SongRequestMetrics { get; private set; }
        public IRaidHistoryRepository RaidHistory { get; private set; }

        public void Dispose()
        {
            _context.Dispose();
        }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public Task<int> SaveChangesAsync()
        {
            return _context.SaveChangesAsync();
        }
    }
}
