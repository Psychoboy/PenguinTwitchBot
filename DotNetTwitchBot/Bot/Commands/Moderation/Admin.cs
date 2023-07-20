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

        public Admin(
            IWebSocketMessenger webSocketMessenger,
            ServiceBackbone serviceBackbone,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _webSocketMessenger = webSocketMessenger;
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (e.isBroadcaster == false) return;
            switch (e.Command)
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

        public override void RegisterDefaultCommands()
        {
            throw new NotImplementedException();
        }
    }
}