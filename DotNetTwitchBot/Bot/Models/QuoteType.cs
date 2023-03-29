using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    public class QuoteType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.MinValue;
        public string CreatedBy { get; set; } = "";
        public string Game { get; set; } = "";
        public string Quote { get; set; } = "";
    }
}