﻿namespace DotNetTwitchBot.Twitch.EventSub.Websockets.Core.EventArgs
{
    internal class DataReceivedArgs : System.EventArgs
    {
        public string Message { get; internal set; } = default!;
    }
}