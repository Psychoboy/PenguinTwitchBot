using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Metrics
{
    public class SongRequests(
        IServiceScopeFactory scopeFactory,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        ILogger<SongRequests> logger
            ) : BaseCommandService(serviceBackbone, commandHandler, "SongRequestsMeetrics"), IHostedService
    {
        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            return Task.CompletedTask;
        }

        public async Task IncrementSongCount(Song song)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.SongRequestHistory.AddAsync(new Models.Metrics.SongRequestHistory
            {
                SongId = song.SongId,
                Title = song.Title,
                Duration = song.Duration
            });
            await db.SaveChangesAsync();
        }

        public async Task DecrementSongCount(Song song)
        {

            var songRequestMetric = await GetLastRequestForSong(song);
            if (songRequestMetric == null) return;

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.SongRequestHistory.Remove(songRequestMetric);
            await db.SaveChangesAsync();
        }

        public async Task<int> GetRequestedCount(Song song)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var songRequestMetric = await db.SongRequestHistoryWithRank.Find(x => x.SongId == song.SongId).FirstOrDefaultAsync();
            return songRequestMetric == null ? 0 : songRequestMetric.RequestedCount;
        }

        private async Task<Models.Metrics.SongRequestHistory?> GetLastRequestForSong(Song song)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.SongRequestHistory.Find(x => x.SongId == song.SongId).OrderByDescending(x => x.RequestDate).FirstOrDefaultAsync();
        }

        public async Task<List<Models.Metrics.SongRequestHistoryWithRank>> GetTopN(int topN)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.SongRequestHistoryWithRank.GetAsync(orderBy: x => x.OrderBy(y => y.Ranking), limit: topN);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {module}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {module}", ModuleName);
            return Task.CompletedTask;
        }
    }
}