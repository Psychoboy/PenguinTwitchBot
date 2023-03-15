using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Bot.Commands.Counters
{
    public abstract class BaseCounter : BaseCommand
    {
        private SendAlerts _sendAlerts;

        protected BaseCounter(ServiceBackbone eventService, SendAlerts sendAlerts) : base(eventService)
        {
            _sendAlerts = sendAlerts;
        }

        public AlertImage? AlertImage { get; set; } = null;
        public abstract string Message();

        public async Task SendCounter()
        {
            await _eventService.SendChatMessage(Message());
            if (AlertImage != null)
            {
                _sendAlerts.QueueAlert(AlertImage);
            }
        }
    }
}