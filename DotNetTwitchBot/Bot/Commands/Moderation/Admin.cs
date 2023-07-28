using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class Admin : BaseCommandService
    {
        private IWebSocketMessenger _webSocketMessenger;
        private Logger<Admin> _logger;

        public Admin(
            Logger<Admin> logger,
            IWebSocketMessenger webSocketMessenger,
            ServiceBackbone serviceBackbone,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _webSocketMessenger = webSocketMessenger;
            _logger = logger;
        }

        public override async Task RegisterDefaultCommands()
        {
            var moduleName = "Admin";
            await RegisterDefaultCommand("pausealerts", this, moduleName, Rank.Streamer);
            await RegisterDefaultCommand("resumealerts", this, moduleName, Rank.Streamer);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
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
            await _serviceBackbone.SendChatMessage("Alerts resumed.");
        }

        public async Task PauseAlerts()
        {
            _webSocketMessenger.Pause();
            await _serviceBackbone.SendChatMessage("Alerts paused.");
        }
    }
}