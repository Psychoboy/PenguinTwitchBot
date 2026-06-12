using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models;
using PenguinTwitchBot.Repository;
using LinqToDB.EntityFrameworkCore;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PenguinTwitchBot.Bot.Commands.Features
{
    public class LoyaltyFeature(
        ILogger<LoyaltyFeature> logger,
        IViewerFeature viewerFeature,
        IServiceScopeFactory scopeFactory,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        Application.Notifications.IPenguinDispatcher dispatcher
            ) : BaseCommandService(serviceBackbone, commandHandler, "LoyaltyFeature", dispatcher), ILoyaltyFeature, IHostedService
    {
        private readonly Timer _intervalTimer = new Timer(60000);
        private static readonly Prometheus.Gauge NumberOfPastiesEarned = Prometheus.Metrics.CreateGauge("number_of_pasties_earned", "Number of Pasties earned since stream start", labelNames: new[] { "viewer" });
        private static readonly Prometheus.Gauge NumberOfPastiesLost = Prometheus.Metrics.CreateGauge("number_of_pasties_lost", "Number of Pasties lost since stream start", labelNames: new[] { "viewer" });
        private static readonly Prometheus.Gauge NumberOfPastiesDiff = Prometheus.Metrics.CreateGauge("number_of_pasties_diff", "Number of Pasties diff since stream start", labelNames: new[] { "viewer" });

        private Task StreamStarted(object? sender, EventArgs _)
        {
            return Task.Run(() =>
            {
                {
                    var labels = NumberOfPastiesEarned.GetAllLabelValues();
                    foreach (var label in labels)
                    {
                        NumberOfPastiesEarned.RemoveLabelled(label);
                    }
                }
                {
                    var labels = NumberOfPastiesLost.GetAllLabelValues();
                    foreach (var label in labels)
                    {
                        NumberOfPastiesLost.RemoveLabelled(label);
                    }
                }
                {
                    var labels = NumberOfPastiesDiff.GetAllLabelValues();
                    foreach (var label in labels)
                    {
                        NumberOfPastiesDiff.RemoveLabelled(label);
                    }
                }
            });
        }

        private async void ElapseTimer(object? sender, ElapsedEventArgs e)
        {
            try
            {
                await UpdatePointsAndTime();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update timer");
            }
        }

        public static Int64 MaxBet { get; } = 200000069;

        public async Task OnChatMessage(ChatMessageEventArgs e)
        {
            if (!ServiceBackbone.IsOnline) return;
            if (ServiceBackbone.IsKnownBotOrCurrentStreamer(e.Name)) return;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var viewer = await db.ViewerMessageCounts.Find(x => x.UserId.Equals(e.UserId)).FirstOrDefaultAsync();
            viewer ??= new ViewerMessageCount
            {
                UserId = e.UserId
            };
            viewer.Username = e.Name;
            viewer.MessageCount++;
            db.ViewerMessageCounts.Update(viewer);
            await db.SaveChangesAsync();
        }

        public async Task UpdatePointsAndTime()
        {
            if (!ServiceBackbone.IsOnline) return;

            var currentViewers = viewerFeature.GetCurrentViewers();

            foreach (var viewerName in currentViewers)
            {
                if (ServiceBackbone.IsKnownBot(viewerName)) continue;
                var userId = await viewerFeature.GetViewerId(viewerName);
                if(userId == null) continue;

                try
                {
                    await AddTimeToViewerByUserId(userId, 60);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Couldn't add time");
                }
            }
        }

        public override Task Register()
        {
            return Task.CompletedTask;
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }
        public async Task<string> GetViewerWatchTime(string user)
        {
            var time = await GetUserTimeAndRank(user);
            return StaticTools.ConvertToCompoundDuration(time.Time);
        }

        private async Task AddTimeToViewerByUserId(string userId, int timeToAdd)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var viewer = await viewerFeature.GetViewerByUserId(userId);
                if (viewer == null) return;
                var viewerTime = await db.ViewersTime.Find(x => x.UserId.Equals(userId)).FirstOrDefaultAsync();
                viewerTime ??= new ViewerTime
                {
                    UserId = userId
                };
                viewerTime.Username = viewer.Username;
                viewerTime.Time += timeToAdd;
                db.ViewersTime.Update(viewerTime);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error adding timer to viewer");
            }

        }

        public async Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name)
        {
            ViewerMessageCountWithRank? viewerMessage;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerMessage = await db.ViewerMessageCounts.GetUserMessageCountWithRankByUsername(name);
            }

            return viewerMessage ?? new ViewerMessageCountWithRank { Ranking = int.MaxValue };
        }

        public async Task<List<ViewerMessageCountWithRank>> GetTopNLoudest(int topN)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.ViewerMessageCounts.GetRankedMessageCounts(limit: topN).ToListAsyncLinqToDB();
        }

        public async Task<ViewerTimeWithRank> GetUserTimeAndRank(string name)
        {
            ViewerTimeWithRank? viewerTime;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerTime = await db.ViewersTime.GetUserTimeWithRankByUsername(name);
            }
            return viewerTime ?? new ViewerTimeWithRank() { Ranking = int.MaxValue };
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            _intervalTimer.Elapsed += ElapseTimer;
            _intervalTimer.Start();

            //Loyalty Stuff
            ServiceBackbone.StreamStarted += StreamStarted;
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            _intervalTimer.Stop();
            _intervalTimer.Elapsed -= ElapseTimer;
            ServiceBackbone.StreamStarted -= StreamStarted;

            return Task.CompletedTask;
        }
    }
}