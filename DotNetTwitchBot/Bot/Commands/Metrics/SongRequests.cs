using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Metrics
{
    public class SongRequests(
        IServiceScopeFactory scopeFactory,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler
            ) : BaseCommandService(serviceBackbone, commandHandler)
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
            var songRequestMetric = await GetRequestedSongMetric(song);
            var create = false;
            if (songRequestMetric == null)
            {
                create = true;
                songRequestMetric = new Models.Metrics.SongRequestMetric
                {
                    SongId = song.SongId,
                    Title = song.Title,
                    Duration = song.Duration,
                };
            }

            songRequestMetric.RequestedCount++;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            if (create)
            {
                await db.SongRequestMetrics.AddAsync(songRequestMetric);
            }
            else
            {
                db.SongRequestMetrics.Update(songRequestMetric);
            }
            await db.SaveChangesAsync();
        }

        public async Task DecrementSongCount(Song song)
        {

            var songRequestMetric = await GetRequestedSongMetric(song);
            if (songRequestMetric == null) return;

            songRequestMetric.RequestedCount--;

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.SongRequestMetrics.Update(songRequestMetric);
            await db.SaveChangesAsync();
        }

        public async Task<int> GetRequestedCount(Song song)
        {
            var songRequestMetric = await GetRequestedSongMetric(song);
            return songRequestMetric == null ? 0 : songRequestMetric.RequestedCount;
        }

        public async Task<List<Models.Metrics.SongRequestMetricsWithRank>> GetTopN(int topN)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.SongRequestMetricsWithRank.GetAsync(orderBy: x => x.OrderBy(y => y.Ranking), limit: topN);
        }

        private async Task<Models.Metrics.SongRequestMetric?> GetRequestedSongMetric(Song song)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.SongRequestMetrics.Find(x => x.SongId == song.SongId).FirstOrDefaultAsync();
        }
    }
}