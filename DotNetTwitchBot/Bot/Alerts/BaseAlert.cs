using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Notifications;

namespace DotNetTwitchBot.Bot.Alerts
{
    public abstract class BaseAlert
    {
        public abstract string Generate();
        public abstract string Generate(string fullConfig);
    }
}