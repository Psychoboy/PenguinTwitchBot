using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public class TicketsFeature : BaseCommandService, ITicketsFeature, IHostedService
    {
        private readonly ILogger<TicketsFeature> _logger;

        readonly Timer _autoPointsTimer;

        private readonly IViewerFeature _viewerFeature;
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly Prometheus.Gauge NumberOfTicketsGained = Prometheus.Metrics.CreateGauge("number_of_tickets_gained", "Number of Tickets gained since last stream start", labelNames: new[] { "viewer" });
        private readonly IServiceBackbone _serviceBackbone;

        public TicketsFeature(
            ILogger<TicketsFeature> logger,
            IServiceBackbone serviceBackbone,
            IServiceScopeFactory scopeFactory,
            IViewerFeature viewerFeature,
            ICommandHandler commandHandler)
            : base(serviceBackbone, commandHandler, "TicketsFeature")
        {
            _logger = logger;

            _autoPointsTimer = new Timer(300000); //5 minutes
            _autoPointsTimer.Elapsed += OnTimerElapsed;
            _viewerFeature = viewerFeature;
            _scopeFactory = scopeFactory;
            _autoPointsTimer.Start();
            _serviceBackbone = serviceBackbone;
            _serviceBackbone.StreamStarted += StreamStarted;

        }

        private Task StreamStarted(object? sender, EventArgs _)
        {
            return Task.Run(() =>
            {
                var labels = NumberOfTicketsGained.GetAllLabelValues();
                foreach (var label in labels)
                {
                    NumberOfTicketsGained.RemoveLabelled(label);
                }
            });
        }

        public async Task GiveTicketsToActiveAndSubsOnlineWithBonus(long amount, long bonusAmount)
        {
            var activeViewers = _viewerFeature.GetActiveViewers();
            var onlineViewers = _viewerFeature.GetCurrentViewers();
            foreach (var viewer in onlineViewers)
            {
                if (activeViewers.Contains(viewer) == false && await _viewerFeature.IsSubscriber(viewer))
                {
                    activeViewers.Add(viewer);
                }
            }
            await GiveTicketsWithBonusToViewers(activeViewers.Distinct(), amount, bonusAmount);
        }

        public async Task GiveTicketsToActiveUsers(long amount)
        {
            var activeViewers = _viewerFeature.GetActiveViewers();
            var onlineViewers = _viewerFeature.GetCurrentViewers();
            foreach (var viewer in onlineViewers)
            {
                if (activeViewers.Contains(viewer) == false && await _viewerFeature.IsSubscriber(viewer))
                {
                    activeViewers.Add(viewer);
                }
            }
            await GiveTicketsWithBonusToViewers(activeViewers.Distinct(), amount, 0);
        }

        private async Task GiveTicketsToAllOnlineViewersWithBonus(long amount, long bonusAmount)
        {
            var viewers = _viewerFeature.GetCurrentViewers();
            await GiveTicketsWithBonusToViewers(viewers, amount, bonusAmount);
        }

        public async Task GiveTicketsWithBonusToViewers(IEnumerable<string> viewers, long amount, long subBonusAmount)
        {
            foreach (var viewer in viewers)
            {
                long bonus = 0;
                var viewerData = await _viewerFeature.GetViewer(viewer);
                if (viewerData != null)
                {
                    bonus = viewerData.isSub ? subBonusAmount : 0; // Sub Bonus
                }
                await GiveTicketsToViewer(viewer, amount + bonus);
            }
        }

        public async Task<long> GiveTicketsToViewer(string viewer, long amount)
        {
            if (ServiceBackbone.IsKnownBot(viewer)) return 0;
            ViewerTicket? viewerPoints;
            var viewerRec = await _viewerFeature.GetViewer(viewer);
            if (viewerRec == null)
            {
                _logger.LogWarning("Couldn't give tickets to viewer, they don't exist {viewer}", viewer);
                return 0;
            }
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerPoints = await db.ViewerTickets.Find(x => x.Username.Equals(viewer)).FirstOrDefaultAsync();
            }
            viewerPoints ??= new ViewerTicket
            {
                UserId = viewerRec.UserId,
                Points = 0
            };
            viewerPoints.Username = viewer;
            viewerPoints.Points += amount;
            if (viewerPoints.Points < 0)
            {
                //Should NEVER hit this
                _logger.LogCritical("Points for {name} would have gone negative, points to remove {points}", viewer.Replace(Environment.NewLine, ""), amount);
                throw new Exception("Points would have went negative. ABORTING");
            }

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.ViewerTickets.Update(viewerPoints);
                await db.SaveChangesAsync();
            }
            if (amount > 0)
            {
                NumberOfTicketsGained.WithLabels(viewer).Inc(amount);
            }

            return viewerPoints.Points;
        }

        public async Task<long> GetViewerTickets(string viewer)
        {

            ViewerTicket? viewerPoints;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerPoints = await db.ViewerTickets.Find(x => x.Username.Equals(viewer)).FirstOrDefaultAsync();
            }
            return viewerPoints == null ? 0 : viewerPoints.Points;
        }

        public async Task<ViewerTicketWithRanks?> GetViewerTicketsWithRank(string viewer)
        {
            ViewerTicketWithRanks? viewerTickets;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                viewerTickets = await db.ViewerTicketsWithRank.Find(x => x.Username.Equals(viewer)).FirstOrDefaultAsync();
            }
            return viewerTickets;
        }

        public async Task<bool> RemoveTicketsFromViewer(string viewer, long amount)
        {
            try
            {
                await GiveTicketsToViewer(viewer, -amount);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (ServiceBackbone.IsOnline)
            {
                _logger.LogInformation("Starting to give  out tickets");
                await GiveTicketsToActiveAndSubsOnlineWithBonus(5, 4);
                await GiveTicketsToAllOnlineViewersWithBonus(1, 2);
            }
        }

        private async Task SayViewerTickets(CommandEventArgs e)
        {
            var tickets = await GetViewerTicketsWithRank(e.Name);
            if (tickets == null)
            {
                await ServiceBackbone.SendChatMessage(e.DisplayName, "You currently don't have any tickets, hang around and you will start getting some.");
            }
            else
            {
                await ServiceBackbone.SendChatMessage(
                    string.Format("@{0}, you have {1} tickets. You are currently ranked #{2}. Use !enter AMOUNT to enter your tickets.",
                    e.DisplayName, tickets.Points, tickets.Ranking
                    ));
            }
        }

        public override async Task Register()
        {
            var moduleName = "TicketsFeature";
            await RegisterDefaultCommand("tickets", this, moduleName);
            await RegisterDefaultCommand("givetickets", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("resettickets", this, moduleName, Rank.Streamer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "tickets":
                    {
                        await SayViewerTickets(e);
                        break;
                    }
                case "givetickets":
                    {
                        if (Int64.TryParse(e.Args[1], out long amount))
                        {
                            var totalPoints = await GiveTicketsToViewer(e.TargetUser, amount);
                            await ServiceBackbone.SendChatMessage(string.Format("Gave {0} {1} tickets, {0} now has {2} tickets.", await _viewerFeature.GetDisplayName(e.TargetUser), amount, totalPoints));
                        }
                        break;
                    }
                case "resettickets":
                    {
                        await ResetAllPoints();
                    }
                    break;
            }
        }

        public async Task ResetAllPoints()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.ViewerTickets.ExecuteDeleteAllAsync();
            await db.SaveChangesAsync();
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