using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs;
using PenguinTwitchBot.TwitchApi.EventSub.Websockets.Extensions;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace PenguinTwitchBot.TwitchApi.EventSub.Websockets.Client
{
    public class WebsocketClient(ILogger<WebsocketClient>? logger = null) : IDisposable
    {
        public bool IsConnected => _webSocket.State == WebSocketState.Open;
        public bool IsFaulted => _webSocket.CloseStatus != WebSocketCloseStatus.Empty && _webSocket.CloseStatus != WebSocketCloseStatus.NormalClosure;
        internal event AsyncEventHandler<DataReceivedEventArgs>? OnDataReceived;
        internal event AsyncEventHandler<ErrorOccuredEventArgs>? OnErrorOccurred;
        private ClientWebSocket _webSocket = new();
        private readonly ILogger<WebsocketClient> _logger = logger ?? NullLogger<WebsocketClient>.Instance;

        public async Task<bool> ConnectAsync(Uri url)
        {
            try
            {
                if (_webSocket.State is WebSocketState.Open or WebSocketState.Connecting)
                    return true;
                if (_webSocket.State is WebSocketState.Closed)  //after a socken is closed it cannot be reopened
                    _webSocket = new();
                await _webSocket.ConnectAsync(url, CancellationToken.None);
#pragma warning disable 4014
                Task.Run(async () => await ProcessDataAsync());
#pragma warning restore 4014
                return IsConnected;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke(this, new ErrorOccuredEventArgs { Exception = ex, Message = ex.Message });
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                if(_webSocket.State is WebSocketState.Open or WebSocketState.Connecting)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke(this, new ErrorOccuredEventArgs { Exception = ex, Message = ex.Message });
                return false;
            }
        }

        private async Task ProcessDataAsync()
        {
            const int bufferLength = 1024 * 4;
            var buffer = new Memory<byte>(new byte[bufferLength]);

            var store = new byte[bufferLength];
            var payloadSize = 0;
            while(IsConnected)
            {
                try
                {
                    ValueWebSocketReceiveResult receiveResult;
                    do
                    {
                        receiveResult = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);

                        if(payloadSize + receiveResult.Count > store.Length)
                        {
                            var newStoreLength = store.Length + Math.Max(bufferLength, receiveResult.Count);
                            var newStore = new byte[newStoreLength];
                            store.AsSpan().CopyTo(newStore);
                            store = newStore;
                        }
                        buffer.Span[..receiveResult.Count].CopyTo(store.AsSpan(payloadSize));
                        payloadSize += receiveResult.Count;

                    } while(!receiveResult.EndOfMessage);

                    switch(receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            if(payloadSize == 0)
                                continue;

                            _ = InvokeOnDataReceived(store.AsSpan(0, payloadSize).ToArray());
                            payloadSize = 0;
                            break;
                        case WebSocketMessageType.Binary:
                            _logger.LogWarning("Received binary message, which is not supported. Ignoring.");
                            break;
                        case WebSocketMessageType.Close:
                            var logLevel = _webSocket.CloseStatus is WebSocketCloseStatus.NormalClosure ? LogLevel.Information : LogLevel.Critical;
                            _logger.LogWebsocketClosed(logLevel, (WebSocketCloseStatus)_webSocket.CloseStatus!, _webSocket.CloseStatusDescription!);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Unknown WebSocket message type: {receiveResult.MessageType}");
                    }

                } catch (Exception ex)
                {
                    OnErrorOccurred?.Invoke(this, new ErrorOccuredEventArgs { Exception = ex, Message = ex.Message });
                    break;
                }
            }
        }

        async Task InvokeOnDataReceived(byte[] data)
        {
            if(OnDataReceived == null)
                return;
            try
            {
                await OnDataReceived.Invoke(this, new DataReceivedEventArgs { Bytes = data });
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Exception occurred while invoking OnDataReceived event.");
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _webSocket.Dispose();
        }
    }
}