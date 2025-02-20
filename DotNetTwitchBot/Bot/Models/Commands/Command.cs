using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Commands;

namespace DotNetTwitchBot.Bot.Models.Commands
{
    public class Command
    {
        public Command(BaseCommandProperties commandProperties, IBaseCommandService commandService)
        {
            CommandProperties = commandProperties;
            CommandService = commandService;
        }
        public BaseCommandProperties CommandProperties { get; set; }
        public IBaseCommandService CommandService { get; set; }
    }
}