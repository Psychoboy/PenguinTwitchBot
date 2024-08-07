﻿using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public interface IViewerFeature
    {
        List<string> GetActiveViewers();
        List<string> GetCurrentViewers();
        Task<string> GetDisplayName(string username);
        Task<Follower?> GetFollowerAsync(string username);
        Task<string> GetNameWithTitle(string username);
        Task<DateTime?> GetUserCreatedAsync(string username);
        Task<Viewer?> GetViewer(int id);
        Task<Viewer?> GetViewer(string username);
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