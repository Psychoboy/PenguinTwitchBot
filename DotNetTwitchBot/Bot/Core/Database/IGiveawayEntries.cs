using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public interface IGiveawayEntries
    {
        int InsertBulk(IEnumerable<GiveawayEntry> entries);
        IEnumerable<GiveawayEntry> FindAll();
        int DeleteAll();
        int Count(string username);
    }
}