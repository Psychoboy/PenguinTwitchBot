using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Notifications;

namespace DotNetTwitchBot.Bot.Alerts
{
    public class AlertImage : BaseAlert
    {

        public string FileName { get; set; } = "";
        public int Duration { get; set; } = 3;
        public float Volume { get; set; } = 0.8F;
        public string CSS { get; set; } = "";
        public string Message { get; set; } = "";

        public override string Generate()
        {
            return string.Format("{{\"alert_image\":\"{0}, {1}, {2:n1}, {3}, {4}\",\"ignoreIsPlaying\":false}}",
            FileName, Duration, Volume, CSS, Message);
        }

        public override string Generate(string fullConfig)
        {
            return string.Format("{{\"alert_image\":\"{0}\",\"ignoreIsPlaying\":false}}", fullConfig);
        }
    }
}