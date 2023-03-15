using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Alerts;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class TestAlerts : BaseCommand
    {
        SendAlerts _sendAlerts;
        Timer _timer;

        public TestAlerts(ServiceBackbone eventService, SendAlerts sendAlerts) : base(eventService)
        {
            _sendAlerts = sendAlerts;
            _timer = new Timer(timerCallback, this, 1000, 1000);
        }

        private static void timerCallback(object? state)
        {
            if (state == null) return;
            var testAlerts = (TestAlerts)state;
            //testAlerts.AddAlert("{\"alert_image\":\"drinks.gif, 6, 1.0,color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;,SuperPenguinTV has had 0 drinks\",\"ignoreIsPlaying\":false}");
            var testAlert = new AlertImage
            {
                FileName = "drinks.gif",
                Duration = 6,
                Volume = 1.0F,
                CSS = "color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;",
                Message = "SuperPenguinTV has had 0 drinks"
            };
            testAlerts.AddAlert(testAlert);
        }

        private void AddAlert(BaseAlert alert)
        {
            _sendAlerts.QueueAlert(alert);
        }

        protected override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }
    }
}