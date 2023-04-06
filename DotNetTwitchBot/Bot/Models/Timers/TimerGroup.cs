using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models.Timers
{
    public class TimerGroup
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string Name {get;set;} = null!;
        public bool Active {get;set;} = true;
        public int IntervalMinimum {get;set;} = 5;
        public int IntervalMaximum {get;set;} = 15;
        public int MinimumMessages {get;set;} = 15;
        public bool Shuffle {get;set;} = true;
        public DateTime LastRun {get;set;}
        public DateTime NextRun {get;set;}
        
        public List<TimerMessage> Messages {get;set;} = new List<TimerMessage>();
    }
}