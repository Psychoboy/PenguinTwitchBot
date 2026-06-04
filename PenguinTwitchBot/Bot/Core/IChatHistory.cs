using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Models;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;

namespace PenguinTwitchBot.Bot.Core
{
    public interface IChatHistory
    {
        Task AddChatMessage(ChatMessageEventArgs e);
        Task DeleteChatMessage(ChannelChatMessageDeleteEventArgs e);
        Task CleanOldLogs();
        Task<PagedDataResponse<ViewerChatHistory>> GetViewerChatMessages(PaginationFilter filter, bool includeCommands);
    }
}