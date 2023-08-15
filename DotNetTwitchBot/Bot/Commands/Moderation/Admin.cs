using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Notifications;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class Admin : BaseCommandService
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
            ) : base(serviceBackbone, commandHandler)
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
            _logger.LogInformation($"Registered commands for {moduleName}");
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
    }
}