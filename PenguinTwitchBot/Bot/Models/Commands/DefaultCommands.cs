using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Bot.Models.Commands
{
    public class DefaultCommand : BaseCommandProperties
    {
        public string CustomCommandName { get; set; } = null!;
        public string ModuleName { get; set; } = null!;
    }
}