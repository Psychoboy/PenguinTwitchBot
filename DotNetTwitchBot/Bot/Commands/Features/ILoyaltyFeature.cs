using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public interface ILoyaltyFeature
    {
        Task AddPointsToViewer(string target, long points);
        Task<long> GetMaxPointsFromUser(string target);
        Task<long> GetMaxPointsFromUser(string target, long max);
        Task<List<ViewerMessageCountWithRank>> GetTopNLoudest(int topN);
        Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name);
        Task<ViewerPoint> GetUserPasties(string Name);
        Task<ViewerPointWithRank> GetUserPastiesAndRank(string name);
        Task<ViewerTimeWithRank> GetUserTimeAndRank(string name);
        Task<string> GetViewerWatchTime(string user);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task<bool> RemovePointsFromUser(string target, long points);
        Task UpdatePointsAndTime();
        Task SetBitsPerTicket(int numberOfBitsPerTicket);
        Task<int> GetBitsPerTicket();
        Task SetTicketsPerSub(int numberOfTicketsPerSub);
        Task<int> GetTicketsPerSub();
        Task OnChatMessage(ChatMessageEventArgs e);
    }
}