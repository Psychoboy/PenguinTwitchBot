using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(UserId))]
    public class ViewerPoint
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = "";
        public long Points { get; set; } = 0;
        public bool banned { get; set; } = false;
    }
}