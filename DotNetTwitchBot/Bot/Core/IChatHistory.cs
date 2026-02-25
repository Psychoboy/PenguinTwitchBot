using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;

namespace DotNetTwitchBot.Bot.Core
{
    public interface IChatHistory
    {
        Task AddChatMessage(ChatMessageEventArgs e);
        Task DeleteChatMessage(ChannelChatMessageDeleteArgs e);
        Task CleanOldLogs();
        Task<PagedDataResponse<ViewerChatHistory>> GetViewerChatMessages(PaginationFilter filter, bool includeCommands);
    }
}