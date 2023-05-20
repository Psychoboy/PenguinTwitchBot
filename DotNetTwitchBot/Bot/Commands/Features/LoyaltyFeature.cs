using System.Data.Common;
using System.ComponentModel.Design;
using System.Drawing;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class LoyaltyFeature : BaseCommand
    {
        private ViewerFeature _viewerFeature;
        private readonly TicketsFeature _ticketsFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer _intervalTimer;
        private readonly ILogger<LoyaltyFeature> _logger;

        public LoyaltyFeature(
            ILogger<LoyaltyFeature> logger,
            ViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            ServiceBackbone eventService,
            TicketsFeature ticketsFeature
            ) : base(eventService)
        {
            _viewerFeature = viewerFeature;
            _ticketsFeature = ticketsFeature;
            _scopeFactory = scopeFactory;
            _intervalTimer = new Timer(60000);
            _intervalTimer.Elapsed += ElapseTimer;
            _intervalTimer.Start();

            _serviceBackbone.ChatMessageEvent += OnChatMessage;

            //Loyalty Stuff
            _serviceBackbone.SubscriptionEvent += OnSubscription;
            _serviceBackbone.SubscriptionGiftEvent += OnSubScriptionGift;
            _serviceBackbone.CheerEvent += OnCheer;

            _logger = logger;
        }



        private async Task OnCheer(object? sender, CheerEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Name) || e.IsAnonymous)
            {
                await _serviceBackbone.SendChatMessage($"Someone just cheered {e.Amount} bits! sptvHype");
                return;
            }
            try
            {
                await _serviceBackbone.SendChatMessage($"{e.DisplayName} just cheered {e.Amount} bits! sptvHype");
                var ticketsToAward = (int)Math.Floor((double)e.Amount / 100);
                if (ticketsToAward < 1) return;
                await _ticketsFeature.GiveTicketsToViewer(e.Name, ticketsToAward);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error OnCheer");
            }
        }

        private async Task OnSubScriptionGift(object? sender, SubscriptionGiftEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Name)) return;
            try
            {
                await _ticketsFeature.GiveTicketsToViewer(e.Name, 5 * e.GiftAmount);
                var message = $"{e.DisplayName} gifted {e.GiftAmount} subscriptions to the channel! sptvHype sptvHype sptvHype";
                if (e.TotalGifted != null && e.TotalGifted > e.GiftAmount)
                {
                    message += $" They have gifted a total of {e.TotalGifted} subs to the channel!";
                }
                await _serviceBackbone.SendChatMessage(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when processing gift subscription for {user}", e.Name);
            }
        }

        private async Task OnSubscription(object? sender, SubscriptionEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Name)) return;
            if (e.IsGift) return;
            try
            {
                await _ticketsFeature.GiveTicketsToViewer(e.Name, 5);
                if (e.Count != null && e.Count > 0)
                {
                    await _serviceBackbone.SendChatMessage($"{e.DisplayName} just subscribed for {e.Count} months in a row sptvHype, If you want SuperPenguinTV to peg the beard just say Peg in chat! Enjoy the extra tickets!");
                }
                else
                {
                    await _serviceBackbone.SendChatMessage($"{e.DisplayName} just subscribed sptvHype, If you want SuperPenguinTV to peg the beard just say Peg in chat! Enjoy the extra tickets!");
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when processing subscription for {user}", e.Name);
            }
        }


        private async void ElapseTimer(object? sender, ElapsedEventArgs e)
        {
            try
            {
                await UpdatePointsAndTime();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update timer");
            }
        }

        public const Int64 MaxBet = 200000069;

        private async Task OnChatMessage(object? sender, ChatMessageEventArgs e)
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

        // private async void timerCallback(object? state)
        // {
        //     if (state == null)
        //     {
        //         _logger.LogError("State was null, state should never be null!");
        //         return;
        //     }

        // }

        private async Task UpdatePointsAndTime()
        {
            var currentViewers = _viewerFeature.GetCurrentViewers();
            //_logger.LogInformation("(Loyalty) Currently a total of {0} viewers", currentViewers.Count());
            if (!_serviceBackbone.IsOnline) return;
            foreach (var viewer in currentViewers)
            {
                if (viewer.Equals(_serviceBackbone.BotName, StringComparison.CurrentCultureIgnoreCase)) continue;
                try
                {
                    await AddPointsToViewer(viewer, 5);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Couldn't add points");
                }
                finally { }
                try
                {
                    await AddTimeToViewer(viewer, 60);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Couldn't add time");
                }
                finally { }
            }
            var activeViewers = _viewerFeature.GetActiveViewers();
            foreach (var viewer in activeViewers)
            {
                await AddPointsToViewer(viewer, 10);
            }
        }



        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "pasties":
                    await SayLoyalty(e);
                    break;
                case "gift":
                case "give":
                    await GiftPasties(e);
                    break;
                case "check":
                    await CheckUsersPasties(e);
                    break;
                case "addpasties":
                    if (!_serviceBackbone.IsBroadcasterOrBot(e.Name)) return;
                    if (e.Args.Count < 2) return;
                    if (string.IsNullOrWhiteSpace(e.TargetUser)) return;
                    if (Int32.TryParse(e.Args[1], out var points))
                    {
                        if (points <= 0) return;
                        await AddPointsToViewer(e.TargetUser, points);
                        var totalPasties = await GetUserPasties(e.TargetUser);
                        await _serviceBackbone.SendChatMessage(string.Format("{0} now has {1} pasties", e.TargetUser, totalPasties.Points));
                    }
                    break;

            }
        }

        private async Task CheckUsersPasties(CommandEventArgs e)
        {
            var pasties = await GetUserPasties(e.TargetUser);
            if (pasties.Points == 0)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, $"{e.TargetUser} has no pasties or doesn't exist.");
            }
            else
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, $"{e.TargetUser} has {pasties.Points.ToString("N0")} pasties.");
            }
        }

        private async Task GiftPasties(CommandEventArgs e)
        {
            if (e.Args.Count < 2 || e.TargetUser.Equals(e.Name))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "to gift Pasties the command is !gift TARGETNAME AMOUNT");
                return;
            }

            var amount = 0L;
            if (!Int64.TryParse(e.Args[1], out amount))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "to gift Pasties the command is !gift TARGETNAME AMOUNT");
                return;
            }

            var target = await _viewerFeature.GetViewer(e.TargetUser);
            if (target == null)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "that viewer is unknown.");
                return;
            }

            if (!(await RemovePointsFromUser(e.Name, amount)))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "you don't have that many points.");
                return;
            }

            await AddPointsToViewer(e.TargetUser, amount);
            await _serviceBackbone.SendChatMessage(string.Format("{0} has given {1} pasties to {2}", await _viewerFeature.GetNameWithTitle(e.Name), amount, e.TargetUser));
        }

        public async Task<Int64> GetMaxPointsFromUser(string target, Int64 max = MaxBet)
        {
            var viewerPoints = await GetUserPasties(target);
            var maxPoints = 0L;
            if (viewerPoints.Points > max)
            {
                maxPoints = max;
            }
            else
            {
                maxPoints = Convert.ToInt64(viewerPoints.Points);
            }
            return maxPoints;
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

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                try
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
                    db.ViewerPoints.Update(viewerPoint);
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding points to viewer");
                }
            }

        }

        private async Task AddTimeToViewer(string viewer, int timeToAdd)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                try
                {

                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var viewerTime = await db.ViewersTime.FirstOrDefaultAsync(x => x.Username.Equals(viewer));
                    if (viewerTime == null)
                    {
                        viewerTime = new ViewerTime
                        {
                            Username = viewer
                        };

                    }
                    viewerTime.Time += timeToAdd;
                    db.ViewersTime.Update(viewerTime);
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding timer to viewer");
                }
            }

        }

        private async Task SayLoyalty(CommandEventArgs e)
        {
            var pasties = await GetUserPastiesAndRank(e.Name);
            var time = await GetUserTimeAndRank(e.Name);
            var messages = await GetUserMessagesAndRank(e.Name);
            await _serviceBackbone.SendChatMessage($"{await _viewerFeature.GetNameWithTitle(e.Name)} Watch time: [{Tools.ConvertToCompoundDuration(time.Time)}] - sptvBacon Pasties: [#{pasties.Ranking}, {pasties.Points.ToString("N0")}] - Messages: [#{messages.Ranking}, {messages.MessageCount.ToString("N0")} Messages]");
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

        public async Task<List<ViewerMessageCountWithRank>> GetTopNLoudest(int topN)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.ViewerMessageCountWithRanks.OrderBy(x => x.Ranking).Take(topN).ToListAsync();
            }
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