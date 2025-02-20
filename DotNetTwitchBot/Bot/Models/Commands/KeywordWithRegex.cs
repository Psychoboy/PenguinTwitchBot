using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models.Commands
{
    public class KeywordWithRegex
    {
        public KeywordWithRegex(KeywordType keyword)
        {
            Keyword = keyword;
        }
        public KeywordType Keyword { get; set; }
        public Regex Regex { get; set; } = null!;
    }
}