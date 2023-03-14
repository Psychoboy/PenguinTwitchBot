using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class TestAlerts : BaseCommand
    {
        private WebSocketMessenger _webSocketMessenger;
        Timer _timer;

        public TestAlerts(ServiceBackbone eventService, WebSocketMessenger webSocketMessenger) : base(eventService)
        {
            _webSocketMessenger = webSocketMessenger;
            _timer = new Timer(timerCallback, this, 1000, 1000);
        }

        private static void timerCallback(object? state)
        {
            if(state == null) return;
            var testAlerts = (TestAlerts)state;
            testAlerts.AddAlert("{\"alert_image\":\"drinks.gif, 6, 1.0,color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;,SuperPenguinTV has had 0 drinks\",\"ignoreIsPlaying\":false}");
            
        }

        private void AddAlert(string v)
        {
            _webSocketMessenger.AddToQueue(v);
        }

        protected override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }
    }
}