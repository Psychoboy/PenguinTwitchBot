using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PenguinTwitchBot.Bot.Models.Chat;

namespace PenguinTwitchBot.Bot.Events.Chat
{
    public class BaseChatEventArgs
    {
        public bool IsSub { get; set; }
        public bool IsMod { get; set; }
        public bool IsVip { get; set; }
        public bool IsBroadcaster { get; set; }
        public string DisplayName { get; set; } = "";
        public string Name { get; set; } = "";
        public string UserId { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public bool FromOwnChannel { get; set; } = true;

        /// <summary>
        /// Ordered message fragments (text, emote, cheermote, mention).
        /// Shared across chat overlay and command/action handlers.
        /// </summary>
        public List<ChatOverlayFragment> Fragments { get; set; } = [];

        /// <summary>Badge references carried on the message event.</summary>
        public List<ChatOverlayBadge> Badges { get; set; } = [];

        /// <summary>
        /// Resolved display color — the user's Twitch color if set, otherwise a
        /// stable generated color assigned by ChatColorService.
        /// </summary>
        public string ResolvedColor { get; set; } = "#FFFFFF";

        public bool IsSubOrHigher()
        {
            return IsSub || IsMod || IsVip || IsBroadcaster;
        }

        public bool IsVipOrHigher()
        {
            return IsVip || IsMod || IsBroadcaster;
        }

        public bool IsModOrHigher()
        {
            return IsMod || IsBroadcaster;
        }
    }
}