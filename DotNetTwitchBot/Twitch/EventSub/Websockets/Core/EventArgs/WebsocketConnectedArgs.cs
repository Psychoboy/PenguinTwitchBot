﻿namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs
{
    public class WebsocketConnectedArgs : System.EventArgs
    {
        public bool IsRequestedReconnect { get; set; }
    }
}