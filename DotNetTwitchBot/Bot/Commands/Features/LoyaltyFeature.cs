using System.Drawing;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class LoyaltyFeature : BaseCommand
    {
        private ViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Timer _intervalTimer;
        private readonly ILogger<LoyaltyFeature> _logger;

        public LoyaltyFeature(
            ILogger<LoyaltyFeature> logger,
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ServiceBackbone eventService
            ) : base(eventService)
        {
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _intervalTimer = new Timer(timerCallback, this, 20000, 60000);
            _serviceBackbone.ChatMessageEvent += OnChangeMessage;
            _logger = logger;
        }

        private async Task OnChangeMessage(object? sender, ChatMessageEventArgs e)
        {
            if (!_serviceBackbone.IsOnline) return;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var viewer = await db.ViewerMessageCounts.Where(x => x.Username.Equals(e.Sender)).FirstOrDefaultAsync();
                if (viewer == null)
                {
                    viewer = new ViewerMessageCount
                    {
                        Username = e.Sender,
                        MessageCount = 0
                    };
                }
                viewer.MessageCount++;
                db.ViewerMessageCounts.Update(viewer);
                await db.SaveChangesAsync();
            }
        }

        private async void timerCallback(object? state)
        {
            if (state == null) return;
            var feature = (LoyaltyFeature)state;
            await feature.UpdatePointsAndTime();
        }

        private async Task UpdatePointsAndTime()
        {
            if (!_serviceBackbone.IsOnline) return;
            var currentViewers = _viewerFeature.GetCurrentViewers();
            var viewers = new List<Viewer>();
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                foreach (var viewerName in currentViewers)
                {
                    var viewer = await db.Viewers.Where(x => x.Username.Equals(viewerName)).FirstOrDefaultAsync();
                    if (viewer == null)
                    {
                        continue;
                    }
                    viewers.Add(viewer);
                }

                //Give points
                //var viewerPoints = await db.ViewerPoints.Where(x => viewers.Any(y => y.Username.Equals(x.Username))).ToListAsync();
                var viewerNames = viewers.Select(x => x.Username);
                var missingViewerNames = viewerNames.ToList();
                var viewerPoints = await db.ViewerPoints.Where(x => viewerNames.Contains(x.Username)).ToListAsync();
                foreach (var viewerPoint in viewerPoints)
                {
                    viewerPoint.Points += 5;
                    missingViewerNames.Remove(viewerPoint.Username);
                }
                foreach (var missingViewerName in missingViewerNames)
                {
                    viewerPoints.Add(new ViewerPoint
                    {
                        Username = missingViewerName,
                        Points = 5
                    });
                }
                db.ViewerPoints.UpdateRange(viewerPoints);

                missingViewerNames = viewerNames.ToList();
                var viewersTime = await db.ViewersTime.Where(x => viewerNames.Contains(x.Username)).ToListAsync();
                foreach (var viewerTime in viewersTime)
                {
                    viewerTime.Time += 60;
                    missingViewerNames.Remove(viewerTime.Username);
                }
                foreach (var missingViewerName in missingViewerNames)
                {
                    viewersTime.Add(new ViewerTime
                    {
                        Username = missingViewerName,
                        Time = 60
                    });
                }

                db.ViewersTime.UpdateRange(viewersTime);
                await db.SaveChangesAsync();
            }

        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "testpasties":
                    await SayLoyalty(e);
                    break;
                case "addpasties":
                    if (!e.isMod) return;
                    if (e.Args.Count < 2) return;
                    if (string.IsNullOrWhiteSpace(e.TargetUser)) return;
                    if (Int64.TryParse(e.Args[1], out var points))
                    {
                        if (points <= 0) return;
                        await AddPointsToViewer(e.TargetUser, points);
                        var totalPasties = await GetUserPasties(e.TargetUser);
                        await _serviceBackbone.SendChatMessage(string.Format("{0} now has {1} pasties", e.TargetUser, totalPasties.Points));
                    }
                    break;

            }
        }

        private async Task<long> GetAndRemoveMaxPointsFromUser(string target, long max = 200000069)
        {
            var viewerPoints = await GetUserPasties(target);
            var toRemove = 0L;
            if (viewerPoints.Points > max)
            {
                toRemove = max;
            }
            else
            {
                toRemove = viewerPoints.Points;
            }
            if (!await RemovePointsFromUser(target, toRemove)) throw new Exception(string.Format("Failed to remove tickets for {0}", target));
            return toRemove;
        }

        public async Task<bool> RemovePointsFromUser(string target, long points)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var viewerPoint = await db.ViewerPoints.FirstOrDefaultAsync(x => x.Username.Equals(target));
                if (viewerPoint == null) return false;
                if (viewerPoint.Points < points) return false;
                viewerPoint.Points -= points;
                if (viewerPoint.Points < 0)
                {
                    viewerPoint.Points = 0;
                    _logger.LogWarning("User: {0} was about to go negative when attempting to remove {1} pasties.", target, points);
                }
                db.ViewerPoints.Update(viewerPoint);
                await db.SaveChangesAsync();
                return true;
            }
        }

        public async Task AddPointsToViewer(string target, long points)
        {
            long totalPoints = 0;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var viewerPoint = await db.ViewerPoints.Where(x => x.Username.Equals(target)).FirstOrDefaultAsync();
                if (viewerPoint == null)
                {
                    viewerPoint = new ViewerPoint
                    {
                        Username = target.ToLower()
                    };
                }
                viewerPoint.Points += points;
                totalPoints = viewerPoint.Points;
                db.ViewerPoints.Update(viewerPoint);
                await db.SaveChangesAsync();
            }
        }

        private async Task SayLoyalty(CommandEventArgs e)
        {
            var pasties = await GetUserPastiesAndRank(e.Name);
            var time = await GetUserTimeAndRank(e.Name);
            var messages = await GetUserMessagesAndRank(e.Name);
            await _serviceBackbone.SendChatMessage(
                e.DisplayName,
                string.Format("Time: [{0}] - sptvBacon Pasties: [#{1}, {2}] - Messages: [#{3}, {4} Messages]",
                Tools.ConvertToCompoundDuration(time.Time), pasties.Ranking, pasties.Points, messages.Ranking, messages.MessageCount));
        }

        private async Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name)
        {
            ViewerMessageCountWithRank? viewerMessage;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                viewerMessage = await db.ViewerMessageCountWithRanks.Where(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }

            return viewerMessage == null ? new ViewerMessageCountWithRank() { Ranking = int.MaxValue } : viewerMessage;
        }

        public async Task<ViewerPoint> GetUserPasties(string Name)
        {
            ViewerPoint? viewerPoint;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                viewerPoint = await db.ViewerPoints.Where(x => x.Username.Equals(Name)).FirstOrDefaultAsync();
            }

            return viewerPoint == null ? new ViewerPoint() : viewerPoint;
        }

        private async Task<ViewerPointWithRank> GetUserPastiesAndRank(string name)
        {
            ViewerPointWithRank? viewerPoints;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                viewerPoints = await db.ViewerPointWithRanks.Where(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }

            return viewerPoints == null ? new ViewerPointWithRank() { Ranking = int.MaxValue } : viewerPoints;
        }

        private async Task<ViewerTimeWithRank> GetUserTimeAndRank(string name)
        {
            ViewerTimeWithRank? viewerTime;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                viewerTime = await db.ViewersTimeWithRank.Where(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }
            return viewerTime == null ? new ViewerTimeWithRank() { Ranking = int.MaxValue } : viewerTime;
        }

        private async Task<int> GetRank(string name)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var viewerPointRank = await db.ViewerPointWithRanks.Where(x => x.Username.Equals(name)).FirstOrDefaultAsync();
                return viewerPointRank == null ? Int32.MaxValue : viewerPointRank.Ranking;
            }
        }
    }
}