using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public interface IViewerFeature
    {
        List<string> GetActiveViewers();
        List<string> GetCurrentViewers();
        Task<string> GetDisplayNameByUsername(string username);
        Task<Follower?> GetFollowerAsync(string username);
        Task<string> GetNameWithTitle(string username);
        Task<DateTime?> GetUserCreatedAsync(string username);
        Task<Viewer?> GetViewerById(int id);
        Task<Viewer?> GetViewerByUserName(string username);
        Task<Viewer?> GetViewerByUserId(string userId);
        Task<Viewer?> GetViewerByUserIdOrName(string userId, string username);
        Task<bool> IsFollower(string username);
        Task<bool> IsModerator(string username);
        Task<bool> IsSubscriber(string username);
        Task OnChatMessage(ChatMessageEventArgs e);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task SaveViewer(Viewer viewer);
        Task<List<Viewer>> SearchForViewer(string name);
    }
}