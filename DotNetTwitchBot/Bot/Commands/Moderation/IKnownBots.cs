using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public interface IKnownBots
    {
        public bool IsStreamerOrBot(string username);
        public bool IsKnownBot(string username);
        public bool IsKnownBotOrCurrentStreamer(string username);
        public Task AddKnownBot(string username);
        public Task AddKnownBot(KnownBot knownBot);
        public Task RemoveKnownBot(KnownBot knownBot);
        public List<KnownBot> GetKnownBots();
        public Task LoadKnownBots();
    }
}