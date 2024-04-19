using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Core
{
    public interface IChatHistory
    {
        Task AddChatMessage(ChatMessageEventArgs e);
        Task<PagedDataResponse<ViewerChatHistory>> GetViewerChatMessages(PaginationFilter filter);
    }
}