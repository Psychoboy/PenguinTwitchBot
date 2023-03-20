using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class KeywordType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Keyword { get; set; } = "";
        public string Response { get; set; } = "";
        public bool IsRegex { get; set; } = false;
        public bool IsCaseSensitive { get; set; } = false;
        public int Cooldown { get; set; } = 5;
        public Regex Regex { get; set; } = null!;

    }
}