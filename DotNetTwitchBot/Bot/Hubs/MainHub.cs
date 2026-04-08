using Microsoft.AspNetCore.SignalR;

namespace DotNetTwitchBot.Bot.Hubs
{
    /// <summary>
    /// Main SignalR Hub for real-time updates to connected clients.
    /// Supports action execution logs and queue statistics updates.
    /// </summary>
    public class MainHub : Hub
    {
    }
}
