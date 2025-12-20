using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Repository;
using MediatR;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class LoyaltyFeature(
        ILogger<LoyaltyFeature> logger,
        IViewerFeature viewerFeature,
        IServiceScopeFactory scopeFactory,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IMediator mediator,
        IPointsSystem pointsSystem
            ) : BaseCommandService(serviceBackbone, commandHandler, "LoyaltyFeature", mediator), ILoyaltyFeature, IHostedService
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
                    await pointsSystem.AddPointsByUserIdAndGame(userId, ModuleName, 5);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Couldn't add points");
                }

                try
                {
                    await AddTimeToViewerByUserId(userId, 60);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Couldn't add time");
                }
            }
            var activeViewers = viewerFeature.GetActiveViewers();
            foreach (var viewerName in activeViewers)
            {
                if (ServiceBackbone.IsKnownBot(viewerName)) continue;
                var userId = await viewerFeature.GetViewerId(viewerName);
                if (userId == null) continue;
                await AddTimeToViewerByUserId(userId, 10);
            }
        }

        public override Task Register()
        {
            var moduleName = "LoyaltyFeature";
            logger.LogInformation("Registered commands for {moduleName}", moduleName);
            return pointsSystem.RegisterDefaultPointForGame(ModuleName);
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
                viewerMessage = await db.ViewerMessageCountsWithRank.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }

            return viewerMessage ?? new ViewerMessageCountWithRank { Ranking = int.MaxValue };
        }

        public async Task<List<ViewerMessageCountWithRank>> GetTopNLoudest(int topN)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.ViewerMessageCountsWithRank.GetAsync(orderBy: x => x.OrderBy(y => y.Ranking), limit: topN);
        }

        public async Task<ViewerTimeWithRank> GetUserTimeAndRank(string name)
        {
            ViewerTimeWithRank? viewerTime;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerTime = await db.ViewersTimeWithRank.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
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