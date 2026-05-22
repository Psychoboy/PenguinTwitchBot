using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Notifications;

namespace PenguinTwitchBot.Bot.Alerts
{
    public abstract class BaseAlert
    {
        public abstract string Generate();
        public abstract string Generate(string fullConfig);
    }
}