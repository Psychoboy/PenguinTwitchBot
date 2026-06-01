using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Models;

namespace PenguinTwitchBot.Bot.Core
{
    public interface IChatHistory
    {
        Task AddChatMessage(ChatMessageEventArgs e);
        Task DeleteChatMessage(ChannelChatMessageDeleteArgs e);
        Task CleanOldLogs();
        Task<PagedDataResponse<ViewerChatHistory>> GetViewerChatMessages(PaginationFilter filter, bool includeCommands);
    }
}