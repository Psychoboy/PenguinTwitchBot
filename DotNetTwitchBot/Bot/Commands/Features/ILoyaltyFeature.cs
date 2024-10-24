using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public interface ILoyaltyFeature
    {
        Task AddPointsToViewerByUserId(string userid, long points);
        Task AddPointsToViewerByUsername(string username, long points);
        Task<long> GetMaxPointsFromUserByUserId(string userid);
        Task<long> GetMaxPointsFromUserByUserId(string userid, long max);
        Task<List<ViewerMessageCountWithRank>> GetTopNLoudest(int topN);
        Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name);
        Task<ViewerPoint> GetUserPastiesByUserId(string userid);
        Task<ViewerPoint> GetUserPastiesByUsername(string username);
        Task<ViewerPointWithRank> GetUserPastiesAndRank(string name);
        Task<ViewerTimeWithRank> GetUserTimeAndRank(string name);
        Task<string> GetViewerWatchTime(string name);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task<bool> RemovePointsFromUserByUserId(string userid, long points);
        Task<bool> RemovePointsFromUserByUserName(string username, long points);
        Task UpdatePointsAndTime();
        Task SetBitsPerTicket(int numberOfBitsPerTicket);
        Task<int> GetBitsPerTicket();
        Task SetTicketsPerSub(int numberOfTicketsPerSub);
        Task<int> GetTicketsPerSub();
        Task OnChatMessage(ChatMessageEventArgs e);
    }
}