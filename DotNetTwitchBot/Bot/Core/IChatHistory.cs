using DotNetTwitchBot.Models;

namespace DotNetTwitchBot.Bot.Core
{
    public interface IChatHistory
    {
        Task<PagedDataResponse<ViewerChatHistory>> GetViewerChatMessages(PaginationFilter filter);
    }
}