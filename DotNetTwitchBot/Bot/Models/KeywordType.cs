using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class KeywordType : BaseCommandProperties
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public new int? Id { get; set; }
        public string Response { get; set; } = "";
        public bool IsRegex { get; set; } = false;
        public bool IsCaseSensitive { get; set; } = false;

    }
}