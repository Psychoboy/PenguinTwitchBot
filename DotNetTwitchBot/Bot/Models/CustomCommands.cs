using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(CommandName))]
    public class CustomCommands : BaseCommandProperties
    {
        [DataType(DataType.MultilineText)]
        public string Response { get; set; } = null!;
    }
}