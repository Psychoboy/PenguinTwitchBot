using Serilog;
using TwitchLib.Client;

namespace DotNetTwitchBot.Bot
{
    public class TwitchMain
    {
        private TwitchClient Client { get; set; }

        public void Initialize() 
        {
            Client = new TwitchClient();
            Client.Initialize(/*credentials here */null, "superpenguintv");
            Client.OnJoinedChannel += Client_OnJoinedChannel;
            Client.OnLeftChannel += Client_OnLeftChannel;
            Client.OnChatCommandReceived += Client_OnChatCommandReceived;
            Client.OnDisconnected += Client_OnDisconnected;
            Client.OnError += Client_OnError;
            Client.OnLog += Client_OnLog;
            Client.OnConnected += Client_OnConnected;
            Client.OnMessageReceived += Client_OnMessageReceived;
            Client.OnConnectionError += Client_OnConnectionError;
        }

        private void Client_OnConnectionError(object? sender, TwitchLib.Client.Events.OnConnectionErrorArgs e)
        {
            throw new NotImplementedException();
        }

        private void Client_OnMessageReceived(object? sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            throw new NotImplementedException();
        }

        private void Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedArgs e)
        {
            throw new NotImplementedException();
        }

        private void Client_OnLog(object? sender, TwitchLib.Client.Events.OnLogArgs e)
        {
            Log.Debug("OnLog: " + e.ToString());
        }

        private void Client_OnError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Client_OnDisconnected(object? sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Client_OnChatCommandReceived(object? sender, TwitchLib.Client.Events.OnChatCommandReceivedArgs e)
        {
            throw new NotImplementedException();
        }

        private void Client_OnLeftChannel(object? sender, TwitchLib.Client.Events.OnLeftChannelArgs e)
        {
            throw new NotImplementedException();
        }

        private void Client_OnJoinedChannel(object? sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
