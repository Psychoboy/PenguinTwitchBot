using Microsoft.AspNetCore.SignalR;

namespace DotNetTwitchBot.Bot.Hubs
{
    /// <summary>
    /// Main SignalR Hub for real-time updates to connected clients.
    /// Supports action execution logs, queue statistics updates, and fishing catch notifications.
    /// </summary>
    public class MainHub : Hub
    {
        /// <summary>
        /// Simple ping method for circuit health validation
        /// </summary>
        public Task<string> Ping()
        {
            return Task.FromResult("pong");
        }

        /// <summary>
        /// Notify all clients about a new fish catch
        /// </summary>
        public async Task BroadcastFishCatch(object fishCatchData)
        {
            await Clients.All.SendAsync("ReceiveFishCatch", fishCatchData);
        }
    }
}
