using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Models
{
    [Index(nameof(Username))]
    public class Viewer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int? Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; } = DateTime.MinValue;
        public bool isSub { get; set; } = false;
        public bool isVip { get; set; } = false;
        public bool isMod { get; set; } = false;
        public bool isBroadcaster { get; set; } = false;

        public string NameWithTitle()
        {
            if (string.IsNullOrWhiteSpace(Title)) return DisplayName;
            return $"[{Title}] {DisplayName}";
        }

    }
}