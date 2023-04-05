using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(Name))]
    public class AutoShoutout
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Name {get;set;} = null!;
        public string? CustomMessage {get;set;}
        public DateTime LastShoutout {get;set;} = DateTime.MinValue;

    }
}