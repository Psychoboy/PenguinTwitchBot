using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(CommandName))]
    public class AudioCommand : BaseCommandProperties
    {
        public string AudioFile { get; set; } = null!;
    }
}