using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class KeywordWithRegex
    {
        public KeywordWithRegex(KeywordType keyword)
        {
            this.Keyword = keyword;
        }
        public KeywordType Keyword { get; set; }
        public Regex Regex { get; set; } = null!;
    }
}