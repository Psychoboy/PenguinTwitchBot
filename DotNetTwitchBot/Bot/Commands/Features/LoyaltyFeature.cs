using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class LoyaltyFeature : BaseCommandService, ILoyaltyFeature, IHostedService
    {
        private readonly IViewerFeature _viewerFeature;
        private readonly ITicketsFeature _ticketsFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Timer _intervalTimer;
        private readonly ILogger<LoyaltyFeature> _logger;
        private static readonly Prometheus.Gauge NumberOfPastiesEarned = Prometheus.Metrics.CreateGauge("number_of_pasties_earned", "Number of Pasties earned since stream start", labelNames: new[] { "viewer" });
        private static readonly Prometheus.Gauge NumberOfPastiesLost = Prometheus.Metrics.CreateGauge("number_of_pasties_lost", "Number of Pasties lost since stream start", labelNames: new[] { "viewer" });
        private static readonly Prometheus.Gauge NumberOfPastiesDiff = Prometheus.Metrics.CreateGauge("number_of_pasties_diff", "Number of Pasties diff since stream start", labelNames: new[] { "viewer" });

        public LoyaltyFeature(
            ILogger<LoyaltyFeature> logger,
            IViewerFeature viewerFeature,
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ITicketsFeature ticketsFeature,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "LoyaltyFeature")
        {
            _viewerFeature = viewerFeature;
            _ticketsFeature = ticketsFeature;
            _scopeFactory = scopeFactory;
            _intervalTimer = new Timer(60000);
            _intervalTimer.Elapsed += ElapseTimer;
            _intervalTimer.Start();

            //Loyalty Stuff
            ServiceBackbone.SubscriptionEvent += OnSubscription;
            ServiceBackbone.SubscriptionGiftEvent += OnSubScriptionGift;
            ServiceBackbone.CheerEvent += OnCheer;
            ServiceBackbone.StreamStarted += StreamStarted;

            _logger = logger;
        }

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
                var bitsPerTicket = await GetBitsPerTicket();
                if (bitsPerTicket > 0)
                {
                    var ticketsToAward = (int)Math.Floor((double)e.Amount / bitsPerTicket);
                    if (ticketsToAward < 1) return;
                    await _ticketsFeature.GiveTicketsToViewer(e.Name, ticketsToAward);
                    _logger.LogInformation("Awarded {name} {tickets} tickets for {amount} of bits", e.Name, ticketsToAward, e.Amount);
                }
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
                var amountToGift = await GetTicketsPerSub() * e.GiftAmount;
                await _ticketsFeature.GiveTicketsToViewer(e.Name, amountToGift);
                _logger.LogInformation("Gave {name} {amountToGift} tickets for gifting {GiftAmount} subs", e.Name, amountToGift, e.GiftAmount);
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
                var subTickets = await GetTicketsPerSub();
                await _ticketsFeature.GiveTicketsToViewer(e.Name, subTickets);
                _logger.LogInformation("Gave {name} {tickets} tickets for subscribing.", e.Name, subTickets);
                var message = $"{e.DisplayName} just subscribed";
                if (e.Count != null && e.Count > 0)
                {
                    message += $" for a total of {e.Count} months";
                }

                if (e.Streak != null && e.Streak > 0)
                {
                    if (e.Count != null && e.Count > 0) message += " and";
                    message += $" for {e.Streak} months in a row";
                }

                message += "! sptvHype";
                await ServiceBackbone.SendChatMessage(message);

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

        public async Task OnChatMessage(ChatMessageEventArgs e)
        {
            if (!ServiceBackbone.IsOnline) return;
            if (ServiceBackbone.IsKnownBotOrCurrentStreamer(e.Name)) return;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var viewer = await db.ViewerMessageCounts.Find(x => x.UserId.Equals(e.UserId)).FirstOrDefaultAsync();
            viewer ??= new ViewerMessageCount
            {
                UserId = e.UserId,
                MessageCount = 0
            };
            viewer.Username = e.Name;
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
                if (ServiceBackbone.IsKnownBot(viewer)) continue;
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
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
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
                    if (long.TryParse(e.Args[1], out var points))
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

            if (!long.TryParse(e.Args[1], out long amount))
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "to gift Pasties the command is !gift TARGETNAME AMOUNT");
                throw new SkipCooldownException();
            }

            var target = await _viewerFeature.GetViewerByUserName(e.TargetUser);
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

        public async Task<long> GetMaxPointsFromUser(string target)
        {
            return await GetMaxPointsFromUser(target, MaxBet);
        }

        public async Task<long> GetMaxPointsFromUser(string target, long max)
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
                _logger.LogWarning("User: {name} was about to go negative when attempting to remove {points} pasties.", target.Replace(Environment.NewLine, ""), points);
            }
            db.ViewerPoints.Update(viewerPoint);
            await db.SaveChangesAsync();
            if (points > 0)
            {
                NumberOfPastiesLost.WithLabels(target).Inc(points);
                NumberOfPastiesDiff.WithLabels(target).Dec(points);
            }
            return true;
        }

        public async Task AddPointsToViewer(string target, long points)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var viewer = await _viewerFeature.GetViewerByUserName(target);
                if(viewer == null)
                {
                    _logger.LogInformation("No viewer record for {target}", target);
                    return;
                }
                var viewerPoint = await db.ViewerPoints.Find(x => x.Username.Equals(target)).FirstOrDefaultAsync();
                viewerPoint ??= new ViewerPoint
                {
                    UserId = viewer.UserId
                };
                viewerPoint.Username = target.ToLower();
                viewerPoint.Points += points;
                db.ViewerPoints.Update(viewerPoint);
                await db.SaveChangesAsync();
                if (points > 0)
                {
                    NumberOfPastiesEarned.WithLabels(target).Inc(points);
                    NumberOfPastiesDiff.WithLabels(target).Inc(points);
                }

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
                var viewerRec = await _viewerFeature.GetViewerByUserName(viewer);
                if(viewerRec == null)
                {
                    _logger.LogWarning("Couldn't get viewer to update time. {viewer}", viewer);
                    return;
                }
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var viewerTime = await db.ViewersTime.Find(x => x.Username.Equals(viewer)).FirstOrDefaultAsync();
                viewerTime ??= new ViewerTime
                {
                    UserId = viewerRec.UserId
                };
                viewerTime.Username = viewer;
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

        public async Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name)
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
            return await db.ViewerMessageCountsWithRank.GetAsync(orderBy: x => x.OrderBy(y => y.Ranking), limit: topN);
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

        public async Task<ViewerPointWithRank> GetUserPastiesAndRank(string name)
        {
            ViewerPointWithRank? viewerPoints;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerPoints = await db.ViewerPointWithRanks.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }

            return viewerPoints ?? new ViewerPointWithRank() { Ranking = int.MaxValue };
        }

        public async Task<ViewerTimeWithRank> GetUserTimeAndRank(string name)
        {
            ViewerTimeWithRank? viewerTime;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerTime = await db.ViewersTimeWithRank.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
            }
            return viewerTime ?? new ViewerTimeWithRank() { Ranking = int.MaxValue };
        }

        public async Task SetBitsPerTicket(int numberOfBitsPerTicket)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bitsPerTicket = (await db.Settings.GetAsync(x => x.Name.Equals("loyalty.bitsperticket"))).FirstOrDefault();
            bitsPerTicket ??= new()
            {
                Name = "loyalty.bitsperticket"
            };
            bitsPerTicket.IntSetting = numberOfBitsPerTicket;
            db.Settings.Update(bitsPerTicket);
            await db.SaveChangesAsync();
        }

        public async Task<int> GetBitsPerTicket()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bitsPerTicket = db.Settings.Find(x => x.Name.Equals("loyalty.bitsperticket")).FirstOrDefault();
            if (bitsPerTicket == null) { return 10; }
            return bitsPerTicket.IntSetting;
        }

        public async Task SetTicketsPerSub(int numberOfTicketsPerSub)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var ticketsPersub = db.Settings.Find(x => x.Name.Equals("loyalty.ticketspersub")).FirstOrDefault();
            ticketsPersub ??= new()
            {
                Name = "loyalty.ticketspersub"
            };
            ticketsPersub.IntSetting = numberOfTicketsPerSub;
            db.Settings.Update(ticketsPersub);
            await db.SaveChangesAsync();
        }

        public async Task<int> GetTicketsPerSub()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var ticketsPersub = db.Settings.Find(x => x.Name.Equals("loyalty.ticketspersub")).FirstOrDefault();
            if (ticketsPersub == null) { return 50; }
            return ticketsPersub.IntSetting;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}