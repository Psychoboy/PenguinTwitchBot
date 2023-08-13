using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Metrics
{
    public class SongRequests : BaseCommandService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SongRequests(
            IServiceScopeFactory scopeFactory,
            ServiceBackbone serviceBackbone,
            CommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _scopeFactory = scopeFactory;
        }

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
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var songRequestMetric = await db.SongRequestMetrics.Where(x => x.SongId == song.SongId).FirstOrDefaultAsync();
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

        public async Task<int> GetRequestedCount(Song song)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var songRequestMetric = await db.SongRequestMetrics.Where(x => x.SongId == song.SongId).FirstOrDefaultAsync();
            return songRequestMetric == null ? 0 : songRequestMetric.RequestedCount;
        }

        public async Task<List<Models.Metrics.SongRequestMetricWithRank>> GetSongRequestMetrics()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.SongRequestMetricsWithRank.OrderBy(x => x.Ranking).ToListAsync();
        }
    }
}