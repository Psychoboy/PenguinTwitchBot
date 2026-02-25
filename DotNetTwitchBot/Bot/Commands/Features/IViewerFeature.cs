using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public interface IViewerFeature
    {
        List<string> GetActiveViewers(); //PlatformType.Twitch
        List<string> GetCurrentViewers(); //PlatformType.Twitch
        Task<string> GetDisplayNameByUsername(string username, PlatformType platform); //PlatformType.Twitch
        Task<Follower?> GetFollowerAsync(string username, PlatformType platform); //PlatformType.Twitch
        Task<string> GetNameWithTitle(string username, PlatformType platform); //PlatformType.Twitch
        Task<DateTime?> GetUserCreatedAsync(string username, PlatformType platform); //PlatformType.Twitch
        Task<Viewer?> GetViewerById(int id);
        Task<Viewer?> GetViewerByUserName(string username, PlatformType platform); //PlatformType.Twitch
        Task<Viewer?> GetViewerByUserId(string userId, PlatformType platform); //PlatformType.Twitch
        Task<string?> GetViewerId(string username, PlatformType platform); //PlatformType.Twitch
        Task<Viewer?> GetViewerByUserIdOrName(string userId, string username, PlatformType platform); //PlatformType.Twitch
        Task<bool> IsFollowerByUsername(string username, PlatformType platform); //PlatformType.Twitch
        Task<bool> IsModerator(string username, PlatformType platform); //PlatformType.Twitch
        Task<bool> IsSubscriber(string username, PlatformType platform); //PlatformType.Twitch
        Task OnChatMessage(ChatMessageEventArgs e);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task SaveViewer(Viewer viewer);
        Task<List<Viewer>> SearchForViewer(string name);
        Task UpdateEditors();
    }
}