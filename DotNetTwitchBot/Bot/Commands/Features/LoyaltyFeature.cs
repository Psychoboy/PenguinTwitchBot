using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Repository;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class LoyaltyFeature : BaseCommandService
    {
        private readonly IViewerFeature _viewerFeature;
        private readonly ITicketsFeature _ticketsFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Timer _intervalTimer;
        private readonly ILogger<LoyaltyFeature> _logger;

        public LoyaltyFeature(
            ILogger<LoyaltyFeature> logger,
            IViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ITicketsFeature ticketsFeature,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _viewerFeature = viewerFeature;
            _ticketsFeature = ticketsFeature;
            _scopeFactory = scopeFactory;
            _intervalTimer = new Timer(60000);
            _intervalTimer.Elapsed += ElapseTimer;
            _intervalTimer.Start();

            ServiceBackbone.ChatMessageEvent += OnChatMessage;

            //Loyalty Stuff
            ServiceBackbone.SubscriptionEvent += OnSubscription;
            ServiceBackbone.SubscriptionGiftEvent += OnSubScriptionGift;
            ServiceBackbone.CheerEvent += OnCheer;

            _logger = logger;
        }



        private async Task OnCheer(object? sender, CheerEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Name) || e.IsAnonymous)
            {
                await ServiceBackbone.SendChatMessage($"Someone just cheered {e.Amount} bits! sptvHype");
                return;
            }
            try
            {
                await ServiceBackbone.SendChatMessage($"{e.DisplayName} just cheered {e.Amount} bits! sptvHype");
                var ticketsToAward = (int)Math.Floor((double)e.Amount / 10);
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
                var amountToGift = 50 * e.GiftAmount;
                await _ticketsFeature.GiveTicketsToViewer(e.Name, amountToGift);
                _logger.LogInformation("Gave {0} {1} tickets for gifting {2} subs", e.Name, amountToGift, e.GiftAmount);
                var message = $"{e.DisplayName} gifted {e.GiftAmount} subscriptions to the channel! sptvHype sptvHype sptvHype";
                if (e.TotalGifted != null && e.TotalGifted > e.GiftAmount)
                {
                    message += $" They have gifted a total of {e.TotalGifted} subs to the channel!";
                }
                await ServiceBackbone.SendChatMessage(message);
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
                await _ticketsFeature.GiveTicketsToViewer(e.Name, 50);
                _logger.LogInformation("Gave {0} 50 tickets for subscribing.", e.Name);
                if (e.Count != null && e.Count > 0)
                {
                    await ServiceBackbone.SendChatMessage($"{e.DisplayName} just subscribed for {e.Count} months in a row sptvHype, If you want SuperPenguinTV to peg the beard just say Peg in chat! Enjoy the extra tickets!");
                }
                else
                {
                    await ServiceBackbone.SendChatMessage($"{e.DisplayName} just subscribed sptvHype, If you want SuperPenguinTV to peg the beard just say Peg in chat! Enjoy the extra tickets!");
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

        public static Int64 MaxBet { get; } = 200000069;

        private async Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            if (!ServiceBackbone.IsOnline) return;
            if (ServiceBackbone.IsKnownBotOrCurrentStreamer(e.Name)) return;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var viewer = await db.ViewerMessageCounts.Find(x => x.Username.Equals(e.Name)).FirstOrDefaultAsync();
            viewer ??= new ViewerMessageCount
            {
                Username = e.Name,
                MessageCount = 0
            };
            viewer.MessageCount++;
            db.ViewerMessageCounts.Update(viewer);
            await db.SaveChangesAsync();
        }

        public async Task UpdatePointsAndTime()
        {
            if (!ServiceBackbone.IsOnline) return;

            var currentViewers = _viewerFeature.GetCurrentViewers();

            foreach (var viewer in currentViewers)
            {
                if (ServiceBackbone.IsKnownBot(viewer)) continue;

                try
                {
                    await AddPointsToViewer(viewer, 5);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Couldn't add points");
                }

                try
                {
                    await AddTimeToViewer(viewer, 60);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Couldn't add time");
                }
            }
            var activeViewers = _viewerFeature.GetActiveViewers();
            foreach (var viewer in activeViewers)
            {
                await AddPointsToViewer(viewer, 10);
            }
        }

        public override async Task Register()
        {
            var moduleName = "LoyaltyFeature";
            await RegisterDefaultCommand("pasties", this, moduleName);
            await RegisterDefaultCommand("gift", this, moduleName);
            await RegisterDefaultCommand("give", this, moduleName);
            await RegisterDefaultCommand("check", this, moduleName);
            await RegisterDefaultCommand("addpasties", this, moduleName, Rank.Streamer);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
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
                    if (e.Args.Count < 2) return;
                    if (string.IsNullOrWhiteSpace(e.TargetUser)) return;
                    if (Int32.TryParse(e.Args[1], out var points))
                    {
                        if (points <= 0) return;
                        await AddPointsToViewer(e.TargetUser, points);
                        var totalPasties = await GetUserPasties(e.TargetUser);
                        await ServiceBackbone.SendChatMessage(string.Format("{0} now has {1} pasties", e.TargetUser, totalPasties.Points));
                    }
                    break;

            }
        }

        private async Task CheckUsersPasties(CommandEventArgs e)
        {
            var pasties = await GetUserPasties(e.TargetUser);
            if (pasties.Points == 0)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, $"{e.TargetUser} has no pasties or doesn't exist.");
            }
            else
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, $"{e.TargetUser} has {pasties.Points:N0} pasties.");
            }
        }

        private async Task GiftPasties(CommandEventArgs e)
        {
            if (e.Args.Count < 2 || e.TargetUser.Equals(e.Name))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "to gift Pasties the command is !gift TARGETNAME AMOUNT");
                throw new SkipCooldownException();
            }

            if (!Int64.TryParse(e.Args[1], out long amount))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "to gift Pasties the command is !gift TARGETNAME AMOUNT");
                throw new SkipCooldownException();
            }

            var target = await _viewerFeature.GetViewer(e.TargetUser);
            if (target == null)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "that viewer is unknown.");
                throw new SkipCooldownException();
            }

            if (!(await RemovePointsFromUser(e.Name, amount)))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "you don't have that many points.");
                throw new SkipCooldownException();
            }

            await AddPointsToViewer(e.TargetUser, amount);
            await ServiceBackbone.SendChatMessage(string.Format("{0} has given {1} pasties to {2}", await _viewerFeature.GetNameWithTitle(e.Name), amount, e.TargetUser));
        }

        public async Task<Int64> GetMaxPointsFromUser(string target)
        {
            return await GetMaxPointsFromUser(target, MaxBet);
        }

        public async Task<Int64> GetMaxPointsFromUser(string target, Int64 max)
        {
            var viewerPoints = await GetUserPasties(target);
            long maxPoints;
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
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var viewerPoint = await db.ViewerPoints.Find(x => x.Username.Equals(target)).FirstOrDefaultAsync();
            if (viewerPoint == null) return false;
            if (viewerPoint.Points < points) return false;
            viewerPoint.Points -= points;
            if (viewerPoint.Points < 0)
            {
                viewerPoint.Points = 0;
                _logger.LogWarning("User: {0} was about to go negative when attempting to remove {1} pasties.", target.Replace(Environment.NewLine, ""), points);
            }
            db.ViewerPoints.Update(viewerPoint);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task AddPointsToViewer(string target, long points)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var viewerPoint = await db.ViewerPoints.Find(x => x.Username.Equals(target)).FirstOrDefaultAsync();
                viewerPoint ??= new ViewerPoint
                {
                    Username = target.ToLower()
                };
                viewerPoint.Points += points;
                db.ViewerPoints.Update(viewerPoint);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding points to viewer");
            }

        }

        public async Task<string> GetViewerWatchTime(string user)
        {
            var time = await GetUserTimeAndRank(user);
            return Tools.ConvertToCompoundDuration(time.Time);
        }

        private async Task AddTimeToViewer(string viewer, int timeToAdd)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            try
            {

                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var viewerTime = await db.ViewersTime.Find(x => x.Username.Equals(viewer)).FirstOrDefaultAsync();
                viewerTime ??= new ViewerTime
                {
                    Username = viewer
                };
                viewerTime.Time += timeToAdd;
                db.ViewersTime.Update(viewerTime);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding timer to viewer");
            }

        }

        private async Task SayLoyalty(CommandEventArgs e)
        {
            var pasties = await GetUserPastiesAndRank(e.Name);
            var time = await GetUserTimeAndRank(e.Name);
            var messages = await GetUserMessagesAndRank(e.Name);
            await ServiceBackbone.SendChatMessage($"{await _viewerFeature.GetNameWithTitle(e.Name)} Watch time: [{Tools.ConvertToCompoundDuration(time.Time)}] - sptvBacon Pasties: [#{pasties.Ranking}, {pasties.Points:N0}] - Messages: [#{messages.Ranking}, {messages.MessageCount:N0} Messages]");
        }

        private async Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name)
        {
            ViewerMessageCountWithRank? viewerMessage;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerMessage = await db.ViewerMessageCountsWithRank.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }

            return viewerMessage ?? new ViewerMessageCountWithRank { Ranking = int.MaxValue };
        }

        public async Task<List<ViewerMessageCountWithRank>> GetTopNLoudest(int topN)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            //return await db.ViewerMessageCountsWithRank.GetTopN(topN);
            return await db.ViewerMessageCountsWithRank.GetAsync(limit: 10);
        }

        public async Task<ViewerPoint> GetUserPasties(string Name)
        {
            ViewerPoint? viewerPoint;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerPoint = await db.ViewerPoints.Find(x => x.Username.Equals(Name)).FirstOrDefaultAsync();
            }

            return viewerPoint ?? new ViewerPoint();
        }

        private async Task<ViewerPointWithRank> GetUserPastiesAndRank(string name)
        {
            ViewerPointWithRank? viewerPoints;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerPoints = await db.ViewerPointWithRanks.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }

            return viewerPoints ?? new ViewerPointWithRank() { Ranking = int.MaxValue };
        }

        private async Task<ViewerTimeWithRank> GetUserTimeAndRank(string name)
        {
            ViewerTimeWithRank? viewerTime;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerTime = await db.ViewersTimeWithRank.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }
            return viewerTime ?? new ViewerTimeWithRank() { Ranking = int.MaxValue };
        }
    }
}