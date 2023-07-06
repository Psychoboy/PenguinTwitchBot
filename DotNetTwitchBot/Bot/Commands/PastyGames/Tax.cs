using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public class Tax : BaseCommand
    {
        // private static bool ShouldRun = false;
        // private static bool DidRun = false;
        Timer _taxTimer;
        private IServiceScopeFactory _scopeFactory;
        private ILogger<Tax> _logger;

        public Tax(
            ILogger<Tax> logger,
            IServiceScopeFactory scopeFactory,
            ServiceBackbone serviceBackbone) : base(serviceBackbone)
        {
            _taxTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
            _taxTimer.Elapsed += Elapsed;
            // _taxTimer.Start();
            _scopeFactory = scopeFactory;
            _serviceBackbone.StreamEnded += OnStreamEnded;
            _serviceBackbone.StreamStarted += OnStreamStarted;
            _logger = logger;
        }

        private Task OnStreamStarted(object? sender)
        {
            _taxTimer.Stop();
            return Task.CompletedTask;
        }

        private Task OnStreamEnded(object? sender)
        {
            _taxTimer.Start();
            return Task.CompletedTask;
        }

        private async void Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (_serviceBackbone.IsOnline)
                {
                    return;
                };

                _taxTimer.Stop();
                _logger.LogInformation("Processing Tax");
                List<Viewer>? viewers;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    viewers = await db.Viewers.Where(x => x.LastSeen < DateTime.Now.AddDays(-1)).ToListAsync();
                }
                if (viewers == null)
                {
                    _logger.LogWarning("Viewers was null when doing taxes");
                    return;
                }
                long totalRemoved = 0;
                foreach (var viewer in viewers)
                {
                    await using (var scope = _scopeFactory.CreateAsyncScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var viewerPoints = await db.ViewerPoints.Where(x => x.Username.Equals(viewer.Username)).FirstOrDefaultAsync();
                        if (viewerPoints == null) continue;
                        if (viewerPoints.Points <= 25000) continue;
                        var toRemove = (long)Math.Floor(viewerPoints.Points * 0.01);
                        toRemove = toRemove > 200000069 ? 200000069 : toRemove;
                        totalRemoved += toRemove;
                        viewerPoints.Points = viewerPoints.Points - toRemove;
                        db.ViewerPoints.Update(viewerPoints);
                        await db.SaveChangesAsync();
                    }
                }
                _logger.LogInformation($"Removed {totalRemoved} pasties via taxes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running taxes");
            }
        }
        protected override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }
    }
}