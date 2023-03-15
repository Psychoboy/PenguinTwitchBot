using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public interface IDatabaseTools
    {
        public Task Backup();
    }
}