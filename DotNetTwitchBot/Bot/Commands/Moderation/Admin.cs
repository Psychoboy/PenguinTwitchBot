using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class Admin : BaseCommand
    {
        private IWebSocketMessenger _webSocketMessenger;

        public Admin(IWebSocketMessenger webSocketMessenger,
            ServiceBackbone serviceBackbone) : base(serviceBackbone)
        {
            _webSocketMessenger = webSocketMessenger;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
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
    }
}