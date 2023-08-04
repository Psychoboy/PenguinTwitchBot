using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class DefaultCommand : BaseCommandProperties
    {
        public string CustomCommandName { get; set; } = null!;
        public string ModuleName { get; set; } = null!;
    }
}