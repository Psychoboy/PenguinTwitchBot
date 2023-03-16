using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Alerts
{
    public class AlertSound : BaseAlert
    {
        public string AudioHook { get; set; } = "";
        public float Volume { get; set; } = 1.0f;
        public bool IgnoreIsPlaying { get; set; } = false;
        public override string Generate()
        {
            return string.Format("{{\"audio_panel_hook\":\"{0}\", \"audio_panel_volume\":{1:n1}, \"ignoreIsPlaying\":{2}}}", AudioHook, Volume, IgnoreIsPlaying.ToString().ToLower());
        }

        public override string Generate(string fullConfig)
        {
            throw new NotImplementedException();
        }
    }
}