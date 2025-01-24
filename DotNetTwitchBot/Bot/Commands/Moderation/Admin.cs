using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Metrics;
using DotNetTwitchBot.Bot.Models.Wheel;
using DotNetTwitchBot.Bot.Notifications;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class Admin : BaseCommandService, IHostedService
    {
        private readonly IWebSocketMessenger _webSocketMessenger;
        private readonly ILogger<Admin> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceBackbone _serviceBackbone;

        public Admin(
            ILogger<Admin> logger,
            IWebSocketMessenger webSocketMessenger,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler,
            IServiceScopeFactory scopeFactory
            ) : base(serviceBackbone, commandHandler, "Admin")
        {
            _webSocketMessenger = webSocketMessenger;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _serviceBackbone = serviceBackbone;
        }

        public override async Task Register()
        {
            var moduleName = "Admin";
            await RegisterDefaultCommand("pausealerts", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("resumealerts", this, moduleName, Rank.Streamer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "pausealerts":
                    await PauseAlerts();
                    break;
                case "resumealerts":
                    await ResumeAlerts();
                    break;
            }

        }

        public void ShowWheel()
        {
            var wheel = new ShowWheel();
            wheel.Items.Add(new WheelProperty { Label = "Test 1"});
            wheel.Items.Add(new WheelProperty { Label = "Test 2" });
            wheel.Items.Add(new WheelProperty { Label = "Test 3" });
            wheel.Items.Add(new WheelProperty { Label = "Test 4", Weight = 1.5f });
            _webSocketMessenger.AddToQueue(JsonSerializer.Serialize(wheel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }

        public void HideWheel()
        {
            var wheel = new HideWheel();
            _webSocketMessenger.AddToQueue(JsonSerializer.Serialize(wheel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }

        public void SpinWheel()
        {
            var wheel = new SpinWheel(1);
            _webSocketMessenger.AddToQueue(JsonSerializer.Serialize(wheel, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }

        public async Task ResumeAlerts()
        {
            _webSocketMessenger.Resume();
            await ServiceBackbone.SendChatMessage("Alerts resumed.");
        }

        public async Task PauseAlerts()
        {
            _webSocketMessenger.Pause();
            await ServiceBackbone.SendChatMessage("Alerts paused.");
        }

        public async Task UpdateSongs()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.SongRequestHistory.ExecuteDeleteAll();
            var oldHistory = await db.SongRequestMetrics.GetAllAsync();
            var songs = new List<SongRequestHistory>();
            foreach (var item in oldHistory)
            {
                for (var i = 0; i < item.RequestedCount; i++) {
                    songs.Add(new SongRequestHistory
                    {
                        SongId = item.SongId,
                        Title = item.Title,
                        Duration = item.Duration,
                    });
                }
            }
            await db.SongRequestHistory.AddRangeAsync(songs);
            await db.SaveChangesAsync();
        } 

        public async Task ReconnectTwitchWebsocket()
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var websocketService = scope.ServiceProvider.GetServices<IHostedService>().OfType<TwitchWebsocketHostedService>().Single();
                await websocketService.ForceReconnect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect manually.");
            }
        }

        public async Task ForceStreamOnline()
        {
            _serviceBackbone.IsOnline = true;
            await _serviceBackbone.OnStreamStarted();
        }

        public async Task ForceStreamOffline()
        {
            _serviceBackbone.IsOnline = false;
            await _serviceBackbone.OnStreamEnded();
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