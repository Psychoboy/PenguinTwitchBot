using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Notifications;

namespace DotNetTwitchBot.Bot.Alerts
{
    public class SendAlerts
    {
        private WebSocketMessenger _webSocketMessenger;
        public SendAlerts(WebSocketMessenger webSocketMessenger)
        {
            _webSocketMessenger = webSocketMessenger;
        }

        public void QueueAlert(BaseAlert alert)
        {
            _webSocketMessenger.AddToQueue(alert.Generate());
        }
    }
}