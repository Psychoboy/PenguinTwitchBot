using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Tax : BaseCommandService, IHostedService
    {
        readonly Timer _taxTimer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<Tax> _logger;
        private readonly IPointsSystem _pointSystem;

        public Tax(
            ILogger<Tax> logger,
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler,
            IPointsSystem pointsSystem
            ) : base(serviceBackbone, commandHandler, "Tax")
        {
            _taxTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
            _taxTimer.Elapsed += Elapsed;
            _scopeFactory = scopeFactory;
            ServiceBackbone.StreamEnded += OnStreamEnded;
            ServiceBackbone.StreamStarted += OnStreamStarted;
            _logger = logger;
            _pointSystem = pointsSystem;
        }

        private Task OnStreamStarted(object? sender, EventArgs _)
        {
            _taxTimer.Stop();
            return Task.CompletedTask;
        }

        private Task OnStreamEnded(object? sender, EventArgs _)
        {
            _taxTimer.Start();
            return Task.CompletedTask;
        }

        private async void Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (ServiceBackbone.IsOnline)
                {
                    return;
                }

                _taxTimer.Stop();
                _logger.LogInformation("Processing Tax");
                List<Viewer>? viewers;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    viewers = await db.Viewers.Find(x => x.LastSeen < DateTime.Now.AddDays(-1)).ToListAsync();
                }
                if (viewers == null)
                {
                    _logger.LogWarning("Viewers was null when doing taxes");
                    return;
                }
                long totalRemoved = 0;
                foreach (var viewer in viewers)
                {
                    //await using var scope = _scopeFactory.CreateAsyncScope();
                    //var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var viewerPoints = await _pointSystem.GetUserPointsByUserIdAndGame(viewer.UserId, ModuleName);
                    //var viewerPoints = await db.ViewerPoints.Find(x => x.Username.Equals(viewer.Username)).FirstOrDefaultAsync();
                    if (viewerPoints == null) continue;
                    if (viewerPoints.Points <= 25000) continue;
                    var toRemove = (long)Math.Floor(viewerPoints.Points * 0.01);
                    toRemove = toRemove > 200000069 ? 200000069 : toRemove;
                    totalRemoved += toRemove;
                    viewerPoints.Points -= toRemove;
                    await _pointSystem.RemovePointsFromUserByUserIdAndGame(viewer.UserId, ModuleName, toRemove);
                    //db.ViewerPoints.Update(viewerPoints);
                    //await db.SaveChangesAsync();
                }
                _logger.LogInformation("Removed {totalRemoved} pasties via taxes", totalRemoved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running taxes");
            }
        }
        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            return _pointSystem.RegisterDefaultPointForGame(ModuleName);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}