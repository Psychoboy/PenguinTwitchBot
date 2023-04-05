using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class WeatherSettings
    {
        public string ApiKey { get; set; } = null!;
        public string DefaultLocation { get; set; } = null!;
    }
}