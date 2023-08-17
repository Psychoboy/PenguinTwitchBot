using DotNetTwitchBot.Bot.DataAccess;
using DotNetTwitchBot.Bot.Repository.Repositories;

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
            ViewerTickets = new TicketRepository(_context);
            ViewerTicketsWithRank = new TicketsWithRankRepository(_context);
            Viewers = new ViewersRepository(_context);
            Followers = new FollowerRepository(_context);
        }

        public IAudioCommandsRepository AudioCommands { get; private set; }
        public IDefaultCommandRepository DefaultCommands { get; private set; }
        public ISongRequestMetricsRepository SongRequestMetrics { get; private set; }
        public IRaidHistoryRepository RaidHistory { get; private set; }
        public ITicketsRepository ViewerTickets { get; private set; }
        public ITicketsWithRankRepository ViewerTicketsWithRank { get; private set; }
        public IViewersRepository Viewers{ get; private set; }
        public IFollowerRepository Followers { get; private set; }

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
