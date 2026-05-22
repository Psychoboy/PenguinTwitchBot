using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Bot.Core.Database
{
    public interface IDatabaseTools
    {
        public Task Backup();
    }
}