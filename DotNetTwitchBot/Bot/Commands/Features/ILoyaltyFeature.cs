using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public interface ILoyaltyFeature
    {
        Task<List<ViewerMessageCountWithRank>> GetTopNLoudest(int topN);
        Task<ViewerMessageCountWithRank> GetUserMessagesAndRank(string name);
        Task<ViewerTimeWithRank> GetUserTimeAndRank(string name);
        Task<string> GetViewerWatchTime(string name);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task UpdatePointsAndTime();
        Task OnChatMessage(ChatMessageEventArgs e);
    }
}