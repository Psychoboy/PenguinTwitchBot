using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PenguinTwitchBot.Bot.Models.Chat;

namespace PenguinTwitchBot.Bot.Events
{
    public class ChannelPointRedeemEventArgs
    {
        public string UserId { get; set; } = string.Empty;
        public string Sender { get; set; } = "";
        public string Username { get; set; } = "";
        public string Title { get; set; } = "";
        public string UserInput { get; set; } = "";
        public string? EmoteUrl { get; set; }
        public List<ChatOverlayFragment> Fragments { get; set; } = [];
    }
}
