using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class WordFilter
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore]
        public int? Id { get; set; }
        public int TimeOutLength { get; set; } = 300;
        public bool IsRegex { get; set; } = false;
        public string Phrase { get; set; } = "";
        public bool IsSilent { get; set; } = false;
        public bool ExcludeRegulars { get; set; } = false;
        public bool ExcludeSubscribers { get; set; } = false;
        public bool ExcludeVips { get; set; } = false;
        public string Message { get; set; } = "Bad phrase used";
        public string BanReason { get; set; } = "Using a blacklisted phrase";
    }
}