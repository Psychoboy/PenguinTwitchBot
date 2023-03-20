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

        public LoyaltyFeature(
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ServiceBackbone eventService
            ) : base(eventService)
        {
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _intervalTimer = new Timer(timerCallback, this, 20000, 60000);
            _eventService.ChatMessageEvent += OnChangeMessage;
        }

        private async Task OnChangeMessage(object? sender, ChatMessageEventArgs e)
        {
            if (!_eventService.IsOnline) return;
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
            if (!_eventService.IsOnline) return;
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
                    await SayPasties(e);
                    break;
                case "addpasties":
                    if (!e.isMod) return;
                    if (e.Args.Count < 2) return;
                    if (string.IsNullOrWhiteSpace(e.TargetUser)) return;
                    if (Int64.TryParse(e.Args[1], out var points))
                    {
                        if (points <= 0) return;
                        await AddPointsToViewer(e.TargetUser, points);
                        var totalPasties = GetUserPasties(e.TargetUser);
                    }
                    break;

            }
        }

        private async Task AddPointsToViewer(string target, long points)
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

            await _eventService.SendChatMessage(target, string.Format("You now have {0} pasties.", totalPoints));
        }

        private async Task SayPasties(CommandEventArgs e)
        {
            var pasties = await GetUserPastiesAndRank(e.Name);
            await _eventService.SendChatMessage(e.DisplayName, string.Format("you have {0} pasties and rank {1}", pasties.Points, pasties.Ranking));
        }

        private async Task<ViewerPoint> GetUserPasties(string Name)
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

            return viewerPoints == null ? new ViewerPointWithRank() : viewerPoints;
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