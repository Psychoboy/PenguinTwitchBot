using System;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Extensions;

namespace PenguinTwitchBot.TwitchApi.EventSub.Websockets.Client
{
    public interface IWebsocketClient : IDisposable
    {
        bool IsConnected { get; }
        bool IsFaulted { get; }
        Task<bool> ConnectAsync(Uri url);
        Task<bool> DisconnectAsync();
        event AsyncEventHandler<DataReceivedEventArgs> OnDataReceived;
        event AsyncEventHandler<ErrorOccurredEventArgs> OnErrorOccurred;
    }
}
